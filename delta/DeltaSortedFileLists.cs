using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spi.Data;

namespace Delta
{
    class DeltaWriter
    {
        public TextWriter outWriter;
        public TextWriter newWriter;
        public TextWriter modWriter;
        public TextWriter delWriter;
    }
    class DeltaSortedFileLists
    {
        private static bool _debug;

        public static void Run(IEnumerable<string> linesA, IEnumerable<string> linesB, DeltaWriter writer, bool debug)
        {
            _debug = debug;

            Task<List<string>> sortedA = Task.Run(() => { return linesA.OrderBy(line => line, StringComparer.OrdinalIgnoreCase).ToList(); });
            Task<List<string>> sortedB = Task.Run(() => { return linesB.OrderBy(line => line, StringComparer.OrdinalIgnoreCase).ToList(); });

            uint diff =
                Spi.Data.Diff.DiffSortedEnumerables<string,string,string>(
                sortedA.Result, 
                sortedB.Result,
                KeySelector: line =>
                {
                    GetFilenameAndRest(line, out string filename, out string rest);
                    return filename;
                },
                KeyComparer: (a,b) =>
                {
                    return String.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                },
                AttributeSelector: line =>
                {
                    GetFilenameAndRest(line, out string filename, out string rest);
                    return rest;
                },
                AttributeComparer: (a,b) =>
                {
                    var aValues = a.Split('\t');
                    var bValues = b.Split('\t');

                    int SizeCmp = String.CompareOrdinal(aValues[0], bValues[0]);
                    if (SizeCmp != 0) return SizeCmp;

                    int lastWriteCmp = String.CompareOrdinal(aValues[3], bValues[3]);
                    if (lastWriteCmp != 0) return lastWriteCmp;

                    return 0;
                },
                OnCompared: (DIFF_STATE state, string lineA, string lineB) =>
                {
                    string filename;
                    string rest;

                    switch (state)
                    {
                        case DIFF_STATE.NEW:    GetFilenameAndRest(lineB, out filename, out rest); writer.newWriter.WriteLine(filename); break;
                        case DIFF_STATE.MODIFY: GetFilenameAndRest(lineB, out filename, out rest); writer.modWriter.WriteLine(filename); break;
                        case DIFF_STATE.DELETE: GetFilenameAndRest(lineA, out filename, out rest); writer.delWriter.WriteLine(filename); break;
                    }
                },
                checkSortorder: true);
        }
        /*
        private static DIFF_COMPARE_RESULT CompareTwoFileLines(string a, string b)
        {
            GetFilenameAndRest(a, out string filenameA, out string restA);
            GetFilenameAndRest(b, out string filenameB, out string restB);

            int cmpName = String.Compare(filenameA, filenameB, StringComparison.OrdinalIgnoreCase);
            int cmpProps = -1;

            DIFF_COMPARE_RESULT result;

            if (cmpName == 0)
            {
                cmpProps = String.Compare(restA, restB, StringComparison.OrdinalIgnoreCase);
                if (cmpProps == 0)
                {
                    result = DIFF_COMPARE_RESULT.EQUAL;
                }
                else
                {
                    result = DIFF_COMPARE_RESULT.MODIFY;
                }
            }
            else if ( cmpName < 0 )
            {
                result = DIFF_COMPARE_RESULT.LESS;
            }
            else
            {
                result = DIFF_COMPARE_RESULT.GREATER;
            }

            Console.Error.WriteLine($"\na [{filenameA}]\nb [{filenameB}]\nintCmp: {cmpName} intCmpProps: {cmpProps} diff: {result.ToString()}");

            return result;
        }
        */
        private static void GetFilenameAndRest(string line, out string filename, out string rest)
        {
            int firstBackslash = line.IndexOf('\t');
            if ( firstBackslash == -1 )
            {
                throw new Exception($"line has no TAB. wrong format. line is: [{line}]");
            }
            filename = line.Substring(0, firstBackslash);
            rest = line.Substring(firstBackslash + 1);
        }
    }
}
