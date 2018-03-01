using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delta
{
    class Opts
    {
        public bool debug;
        public string fileA;
        public string fileB;
        public string outfile;
        public string newfile;
        public string modfile;
        public string delfile;
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

                using (StreamReader streamA = new StreamReader(opts.fileA, Encoding.Unicode))
                using (StreamReader streamB = new StreamReader(opts.fileB, Encoding.Unicode))
                using (TextWriter outWriter = new StreamWriter(opts.outfile, append: false, encoding: streamA.CurrentEncoding))
                using (TextWriter newWriter = new StreamWriter(opts.newfile, append: false, encoding: streamA.CurrentEncoding))
                using (TextWriter modWriter = new StreamWriter(opts.modfile, append: false, encoding: streamA.CurrentEncoding))
                using (TextWriter delWriter = new StreamWriter(opts.delfile, append: false, encoding: streamA.CurrentEncoding))
                {
                    DeltaSortedFileLists.Run(
                        linesA: StringTools.ReadLines(streamA),
                        linesB: StringTools.ReadLines(streamB),
                        writer: new DeltaWriter()
                        {
                            outWriter = outWriter,
                            newWriter = newWriter,
                            modWriter = modWriter,
                            delWriter = delWriter
                        }, 
                        debug: opts.debug);

                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 99;

            }
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
        }
        static Opts GetOpts(string[] args)
        {
            bool show_help = false;
            bool wrong_files_given = false;

            Opts opts = new Opts();
            var p = new Mono.Options.OptionSet() {
                { "o|out=",     "filename for result of compare",           v => opts.outfile = v },
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
