using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delta
{
    public struct TSV_DATA
    {
        public string relativeFilename;
        public ulong size;
        public uint dwFileAttributes;
        public long timeCreate;
        public long timeModified;
        public long timeAccess;

        public static TSV_DATA Parse(string line)
        {
            string[] col = line.Split('\t');

            return new TSV_DATA()
            {
                relativeFilename = col[0],
                size = ulong.Parse(col[1]),
                dwFileAttributes = uint.Parse(col[2]),
                timeCreate = long.Parse(col[3]),
                timeModified = long.Parse(col[4]),
                timeAccess = long.Parse(col[5])
            };
        }
        public override string ToString()
        {
            return $"{relativeFilename}\t{size}\t{dwFileAttributes}\t{timeCreate}\t{timeModified}\t{timeAccess}";
        }
    }
}
