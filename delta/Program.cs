using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spi;

namespace Delta
{
    class Opts
    {
        public bool debug;
        public bool writeOutSorted;
        public string fileA;
        public string fileB;
        public string outfile;
        public string newfile;
        public string modfile;
        public string delfile;
        public string newdir;
        public string deldir;
    }

    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Opts opts;
                if ((opts = GetOpts(args)) == null)
                {
                    return 8;
                }

                SetDefaults(ref opts);

                using (FileStream streamA = new FileStream(opts.fileA, FileMode.Open))
                using (FileStream streamB = new FileStream(opts.fileB, FileMode.Open))
                using (TextWriter newWriter = new StreamWriter(opts.newfile, append: false, encoding: Encoding.UTF8))
                using (TextWriter modWriter = new StreamWriter(opts.modfile, append: false, encoding: Encoding.UTF8))
                using (TextWriter delWriter = new StreamWriter(opts.delfile, append: false, encoding: Encoding.UTF8))
                {
                    Stats stats = DeltaSortedFileLists.Run(
                        findA: StringTools.ReadLines(streamA, $"ReadLines A {opts.fileA}\t").Select(line => TSV_DATA.Parse(line)),
                        findB: StringTools.ReadLines(streamB, $"ReadLines B {opts.fileB}\t").Select(line => TSV_DATA.Parse(line)),
                        writer: new DeltaWriter()
                        {
                            newFilesWriter = newWriter,
                            modFilesWriter = modWriter,
                            delFilesWriter = delWriter
                        },
                        newDirs: out List<string> newDirs,
                        delDirs: out List<string> delDirs,
                        sortedAfilename: opts.writeOutSorted ? GetSortedFilename(opts.fileA) : null,
                        sortedBfilename: opts.writeOutSorted ? GetSortedFilename(opts.fileB) : null);

                    File.WriteAllLines(path: opts.newdir, contents: newDirs, encoding: Encoding.UTF8);
                    File.WriteAllLines(path: opts.deldir, contents: delDirs, encoding: Encoding.UTF8);

                    WriteStats(stats, newDirs.Count, delDirs.Count);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);

                if ( ex.InnerException != null)
                {
                    Console.Error.WriteLine("----- inner exception");
                    Console.Error.WriteLine(ex.InnerException.Message);
                    Console.Error.WriteLine(ex.InnerException.StackTrace);
                    Console.Error.WriteLine("----- inner exception");
                }

                return 99;

            }
        }

        private static void WriteStats(Stats stats, int newDirsCount, int delDirsCount)
        {
            Console.Error.WriteLine(
                $"\nnew files\t{stats.newFiles,12}\t{Misc.GetPrettyFilesize(stats.newFilesSize)}"
              + $"\nmod files\t{stats.modFiles,12}\t{Misc.GetPrettyFilesize(stats.modFilesSize)}"
              + $"\ndel files\t{stats.delFiles,12}\t{Misc.GetPrettyFilesize(stats.delFilesSize)}"
              + $"\nnew dirs \t{newDirsCount,12}"
              + $"\ndel dirs \t{delDirsCount,12}"
              );
        }

        static string GetSortedFilename(string datafilename)
        {
            return datafilename + "_SORTED" + Path.GetExtension(datafilename);
        }
        static void SetDefaults(ref Opts opts)
        {
            if ( String.IsNullOrEmpty(opts.outfile) )
            {
                opts.outfile = @".\delta.out.txt";
            }
            opts.newfile = @".\delta.new.txt";
            opts.modfile = @".\delta.mod.txt";
            opts.delfile = @".\delta.del.txt";
            opts.newdir  = @".\delta.NewDirs.txt";
            opts.deldir  = @".\delta.DelDirs.txt";
        }
        static Opts GetOpts(string[] args)
        {
            bool show_help = false;
            bool wrong_files_given = false;

            Opts opts = new Opts();
            var p = new Mono.Options.OptionSet() {
                { "o|out=",     "filename for result of compare",           v => opts.outfile = v },
                { "s|sorted",   "write out sorted files",                   v => opts.writeOutSorted = v != null },
                { "d|debug",    "debug",                                    v => opts.debug = v != null },
                { "h|help",     "show this message and exit",               v => show_help = v != null },
            };
            try
            {
                List<string> twoFiles = p.Parse(args);
                if ( twoFiles.Count == 2 )
                {
                    opts.fileA = twoFiles[0];
                    opts.fileB = twoFiles[1];
                }
                else
                {
                    Console.Error.WriteLine("you should supply 2 filenames to compare");
                    wrong_files_given = true;
                }

            }
            catch (Mono.Options.OptionException oex)
            {
                Console.WriteLine(oex.Message);
                return null;
            }
            if (show_help | wrong_files_given)
            {
                ShowHelp(p);
                return null;
            }
            return opts;
        }
        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: delta [OPTIONS] {fileA} {fileB}");
            Console.WriteLine("cmp a with b and writes out the delta");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
