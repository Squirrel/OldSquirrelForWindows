using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NSync.Core
{
    public class ReleaseEntry : IEnableLogger
    {
        public string SHA1 { get; protected set; }
        public string Filename { get; protected set; }
        public long Filesize { get; protected set; }
        public bool IsDelta { get; protected set; }

        protected ReleaseEntry(string sha1, string filename, long filesize, bool isDelta)
        {
            SHA1 = sha1; Filename = filename; Filesize = filesize; IsDelta = isDelta;
        }

        static readonly Regex entryRegex = new Regex(@"^([0-9a-fA-F]{40})\s+(\S+)\s+(\d+)$");
        public static ReleaseEntry ParseReleaseEntry(string entry)
        {
            var m = entryRegex.Match(entry);
            if (!m.Success) {
                return null;
            }

            if (m.Groups.Count != 4) {
                return null;
            }

            long size = Int64.Parse(m.Groups[3].Value);
            bool isDelta = m.Groups[2].Value.EndsWith(".delta", StringComparison.InvariantCultureIgnoreCase);
            return new ReleaseEntry(m.Groups[1].Value, m.Groups[2].Value, size, isDelta);
        }
    }
}