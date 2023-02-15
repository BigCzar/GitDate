using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GitDate
{
    class Program
    {
        static List<ChangeFile> changeFileList;

        static void Main(string[] args)
        {
            try
            {
                // путь к папке репозитория
                string repositoryDir = "C:\\Users\\Czar\\Desktop\\OrionSaveEditor\\test3\\";
                // string repositoryDir = "C:\\Users\\Czar\\source\\repos\\multiterminal";

                #region Cписок файлов в папке репозитория (до изменения даты последней модификации)

                // тут могут быть и "лишние" элементы: файлы самой GIT или случайно попавшие в папку файлы
                // но так как мы будем искать их в изменениях коммитов
                // то там мы не найдём совпадений с этими элементами
                string[] allFilesAbs = Directory.GetFiles(repositoryDir, "*", SearchOption.AllDirectories);
                // вырежим хотябы часть мусора, то что точно не попадёт в коммиты, папку /.git/
                var allRepositoryFile = allFilesAbs.ToList().Select(f => new RepositoryFile(repositoryDir, f)).Where(rf => rf.RelativePath.IndexOf(".git") != 0).ToList();
                Console.WriteLine($"\n Файлы до изменения ({allRepositoryFile.Count()}):\n");
                allRepositoryFile.ForEach(Console.WriteLine);

                #endregion
                #region Список изменений вытащенный из коммитов (Commits) репозитория

                // Воспользуемся пакетом LibGit2Sharp

                var repo = new Repository(repositoryDir, new RepositoryOptions());

                // отсортеруем коммиты по времени по убыванию
                // что бы при нахождении первого вхождения файла не надо было искать дальше
                // потомучто дальше могут быть только его более раннии модификации
                var orderedCommitLog = repo.Commits.OrderByDescending(c => c.Committer.When);

                // список изменений из коммитов
                changeFileList = new List<ChangeFile>();

                Console.WriteLine("\n\n Изменения из Commits:\n");

                // заполняем список changeFileList из отсортированных коммитов - orderedCommitLog
                foreach (Commit commit in orderedCommitLog)
                {
                    foreach (var parent in commit.Parents)
                    {
                        DateTimeOffset t = commit.Committer.When.ToLocalTime();
                        Console.WriteLine("\n Commit: {0} | {1}\n", t, commit.MessageShort);
                        foreach (TreeEntryChanges change in repo.Diff.Compare<TreeChanges>(parent.Tree,
                        commit.Tree))
                        {
                            Console.WriteLine("{0}: {1}", change.Status, change.Path);
                            changeFileList.Add(new ChangeFile(change.Path, t));
                        }
                    }
                }

                #endregion
                #region Найдём даты последних модификаций файлов и заменим на них

                // будем считать что дата последней модификации файла это дата последнего коммита
                // в который попал данный файл

                Console.WriteLine("\n\n Найдены даты последнего изменения:\n");
                allRepositoryFile.ForEach(FindChange);

                #endregion

                Console.WriteLine("\n Файлы после изменения:\n");
                allRepositoryFile.ForEach(Console.WriteLine);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Ищет файл в изменениях коммита и если находит, то  задаёт ему время последнего изменения из коммита
        /// </summary>
        /// <param name="repoFile"></param>
        private static void FindChange(RepositoryFile repoFile)
        {
            foreach (var changeFile in changeFileList)
            {
                if (changeFile.Path == repoFile.RelativePath)
                {
                    Console.WriteLine($"File: {repoFile.RelativePath}: {repoFile.LastWriteTime} --> {changeFile.WriteTime}");
                    repoFile.SetLastWriteTime(changeFile.WriteTime);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Файл репозитория
    /// </summary>
    public class RepositoryFile
    {
        public string Directory { get; set; }
        public string AbsolutePath { get; set; }
        public string RelativePath { get; set; }
        public DateTime LastWriteTime
        {
            get { return File.GetLastWriteTime(AbsolutePath); }
        }

        public RepositoryFile(string Directory, string AbsolutePath)
        {
            this.Directory = Directory.Replace("\\", "/");
            this.AbsolutePath = AbsolutePath.Replace("\\", "/");
            RelativePath = AbsolutePath.Replace(Directory, string.Empty).Replace("\\", "/");
        }

        public override string ToString()
        {
            return $"{LastWriteTime}: {RelativePath}";
        }
        /// <summary>
        /// Изменяет дату последнего изменения файла
        /// </summary>
        /// <param name="writeTime">Новая дата полседнего изменения</param>
        internal void SetLastWriteTime(DateTime writeTime)
        {
            File.SetLastWriteTime(AbsolutePath, writeTime);
        }
    }
    /// <summary>
    /// Изменение файла из коммита
    /// </summary>
    public class ChangeFile
    {
        public string Path { get; set; }
        public DateTime WriteTime { get; set; }

        public ChangeFile(string Path, DateTimeOffset WriteTime)
        {
            this.Path = Path;
            this.WriteTime = WriteTime.DateTime;
        }
    }
}
