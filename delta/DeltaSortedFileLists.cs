using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spi;
using Spi.Data;

namespace Delta
{
    public class DeltaWriter
    {
        //public TextWriter outWriter;
        public TextWriter newFilesWriter;
        public TextWriter modFilesWriter;
        public TextWriter delFilesWriter;
    }
    public class Stats
    {
        public ulong newFiles;
        public ulong newFilesSize;
        public ulong modFiles;
        public ulong modFilesSize;
        public ulong delFiles;
        public ulong delFilesSize;
    }
    public class DeltaSortedFileLists
    {
        private static bool _debug;

        public static Stats Run(IEnumerable<TSV_DATA> findA, IEnumerable<TSV_DATA> findB, DeltaWriter writer, 
            out List<string> newDirs, out List<string> delDirs,
            string sortedAfilename, string sortedBfilename)
        {
            newDirs = null;
            delDirs = null;
            Stats stats = null;

            List<TSV_DATA> newFiles;
            List<TSV_DATA> delFiles;

            Console.Error.WriteLine("trying diff on given data");
            bool wasSorted = true;
            try
            {
                stats = DoDiff(findA, findB, writer, out newDirs, out delDirs, out newFiles, out delFiles);
            }
            catch (ApplicationException appEx)
            {
                wasSorted = false;
                Console.Error.WriteLine(appEx.Message);
                Console.Error.WriteLine("data was not sorted!");
            }

            Task writeSortedA = Task.CompletedTask;
            Task writeSortedB = Task.CompletedTask;
            if (!wasSorted)
            {
                Console.Error.WriteLine("reading files again and sorting");
                Task<List<TSV_DATA>> sortedA = SortData(findA, "A");
                Task<List<TSV_DATA>> sortedB = SortData(findB, "B");

                while (!Task.WaitAll(new Task[] { sortedA, sortedB }, millisecondsTimeout: 5000))
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.GetCurrentProcess();
                        Console.Error.WriteLine($"virtMem: {Misc.GetPrettyFilesize(proc.VirtualMemorySize64)}");
                    }
                    catch { }
                }
                Console.Error.WriteLine($"number sorted items A/B: {sortedA.Result.Count}/{sortedB.Result.Count}");

                writeSortedA = (sortedAfilename == null) ? Task.CompletedTask : WriteTsvData(sortedAfilename, sortedA.Result);
                writeSortedB = (sortedBfilename == null) ? Task.CompletedTask : WriteTsvData(sortedBfilename, sortedB.Result);

                Console.Error.WriteLine("running diff on sorted data");
                stats = DoDiff(sortedA.Result, sortedB.Result, writer, out newDirs, out delDirs, out newFiles, out delFiles);


            }

            File.WriteAllLines(@".\DelDirsBeforeCompress.txt", delDirs);
            delDirs.Sort();
            IEnumerable<string> compressDelDirs = CompressToBaseDirs(delDirs);
            delDirs = compressDelDirs.ToList();

            Task.WaitAll(new Task[] { writeSortedA, writeSortedB });

            return stats;
        }
        public static IEnumerable<string> CompressToBaseDirs(IEnumerable<string> sortedDirs)
        {
            string shortDir = null;
            foreach ( string dir in sortedDirs)
            {
                if (shortDir==null)
                {
                    shortDir = dir;
                    continue;
                }

                int cmp = String.Compare(strA: shortDir, indexA: 0, strB: dir, indexB: 0, length: shortDir.Length, ignoreCase: true);
                if ( cmp < 0 )
                {
                    yield return shortDir;
                    shortDir = dir;
                }
                else if ( cmp > 0 )
                {
                    throw new Exception(
                        $"CompressDelDirs: dirname curr < last"
                        + $"\ncurr [{dir}]"
                        + $"\nlast [{shortDir}]");
                }
            }
            if (shortDir == null)
            {
                yield break;
            }
            else
            {
                yield return shortDir;
            }
        }
        private static Stats DoDiff(IEnumerable<TSV_DATA> sortedA, IEnumerable<TSV_DATA> sortedB, DeltaWriter writer, out List<string> newDirs, out List<string> delDirs,
            out List<TSV_DATA> newFiles, out List<TSV_DATA> delFiles)
        {
            List<string> tmpNewDirs = new List<string>();
            List<string> tmpDelDirs = new List<string>();
            List<TSV_DATA> tmpNewFiles = new List<TSV_DATA>();
            List<TSV_DATA> tmpDelFiles = new List<TSV_DATA>();

            Stats stats = new Stats();

            uint diff =
                Spi.Data.Diff.DiffSortedEnumerables<TSV_DATA>(
                sortedA,
                sortedB,
                KeyComparer: (TSV_DATA a, TSV_DATA b) =>
                {
                    int cmp = String.Compare(a.relativeFilename, b.relativeFilename, StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0)
                    {
                        return cmp;
                    }
                    bool KindOfA = Spi.Misc.IsDirectoryFlagSet(a.dwFileAttributes);
                    bool KindOfB = Spi.Misc.IsDirectoryFlagSet(b.dwFileAttributes);

                    return KindOfA == KindOfB
                                ?  0    // two directories OR two files --> same name --> return 0 
                                : -1;   // one dir AND one file         --> same name --> return -1 to represent the difference
                },
                AttributeComparer: (TSV_DATA a, TSV_DATA b) =>
                {
                    if (Misc.IsDirectoryFlagSet(a.dwFileAttributes) && Misc.IsDirectoryFlagSet(b.dwFileAttributes))
                    {
                        return 0;
                    }
                    long cmp;
                    if ((cmp = (a.timeModified - b.timeModified)) != 0)
                    {
                        return (int)cmp;
                    }
                    if ((cmp = (long)(a.size - b.size)) != 0)
                    {
                        return (int)cmp;
                    }

                    return 0;
                },
                OnCompared: (DIFF_STATE state, TSV_DATA a, TSV_DATA b) =>
                {
                    switch (state)
                    {
                        case DIFF_STATE.NEW:
                            if (Misc.IsDirectoryFlagSet(b.dwFileAttributes))
                            {
                                tmpNewDirs.Add(b.relativeFilename);
                            }
                            else
                            {
                                writer.newFilesWriter.WriteLine(b.relativeFilename);
                                tmpNewFiles.Add(b);
                                stats.newFiles += 1;
                                stats.newFilesSize += b.size;
                            }
                            break;
                        case DIFF_STATE.MODIFY:
                            writer.modFilesWriter.WriteLine(b.relativeFilename);
                            stats.modFiles += 1;
                            stats.modFilesSize += b.size;
                            break;
                        case DIFF_STATE.DELETE:
                            if (Misc.IsDirectoryFlagSet(a.dwFileAttributes))
                            {
                                tmpDelDirs.Add(a.relativeFilename);
                            }
                            else
                            {
                                writer.delFilesWriter.WriteLine(a.relativeFilename);
                                tmpDelFiles.Add(a);
                                stats.delFiles += 1;
                                stats.delFilesSize += a.size;
                            }
                            break;
                    }
                },
                checkSortOrder: true);

            newDirs = tmpNewDirs;
            delDirs = tmpDelDirs;

            newFiles = tmpNewFiles;
            delFiles = tmpDelFiles;

            return stats;
        }
        private static Task<List<TSV_DATA>> SortData(IEnumerable<TSV_DATA> data, string label)
        {
            return
                Task.Run(
                    () => 
                    {
                        Console.Error.WriteLine($"reading {label}");
                        List<TSV_DATA> memData = data.ToList();
                        Console.Error.WriteLine($"sorting {label}");
                        memData.Sort((a, b) => String.Compare(a.relativeFilename, b.relativeFilename, StringComparison.OrdinalIgnoreCase));
                        return memData;
                    });
        }
        static Task WriteTsvData(string filename, IEnumerable<TSV_DATA> find_data)
        {
            return Task.Run(() =>
            {
                Console.Error.WriteLine($"writing data to {filename}");
                File.WriteAllLines(filename, find_data.Select(item => item.ToString()), Encoding.UTF8);
            });
        }
    }
}
