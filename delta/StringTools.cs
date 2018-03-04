using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delta
{
    class StringTools
    {
        public static IEnumerable<string> ReadLines(FileStream filestream, string context)
        {
            Console.Error.WriteLine($"{context}: seek(0)");
            filestream.Seek(0, SeekOrigin.Begin);

            ulong countLines = 0;
            string line;
            TextReader reader = new StreamReader(filestream, detectEncodingFromByteOrderMarks: true);
            while ((line = reader.ReadLine()) != null)
            {
                countLines += 1;
                yield return line;
            }
            Console.Error.WriteLine($"{context}: {countLines}");
        }
    }
}
