using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
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
            Contract.Requires(sha1 != null && sha1.Length == 40);
            Contract.Requires(filename != null);
            Contract.Requires(filename.Contains(Path.DirectorySeparatorChar) == false);
            Contract.Requires(filesize > 0);

            SHA1 = sha1; Filename = filename; Filesize = filesize; IsDelta = isDelta;
        }

        public string EntryAsString {
            get { return String.Format("{0} {1} {2}", SHA1, Filename, Filesize); } 
        }

        public Version Version {
            get {
                var parts = (new FileInfo(Filename)).Name
                    .Replace(".nupkg", "").Replace("-delta", "")
                    .Split('.', '-').Reverse();

                var numberRegex = new Regex(@"^\d+$");

                var versionFields = parts
                    .Where(x => numberRegex.IsMatch(x))
                    .Select(Int32.Parse)
                    .Reverse()
                    .ToArray();

                if (versionFields.Length < 2 || versionFields.Length > 4) {
                    return null;
                }

                switch(versionFields.Length) {
                case 2:
                    return new Version(versionFields[0], versionFields[1]);
                case 3:
                    return new Version(versionFields[0], versionFields[1], versionFields[2]);
                case 4:
                    return new Version(versionFields[0], versionFields[1], versionFields[2], versionFields[3]);
                }

                return null;
            } 
        }

        static readonly Regex entryRegex = new Regex(@"^([0-9a-fA-F]{40})\s+(\S+)\s+(\d+)[\r]*$");
        public static ReleaseEntry ParseReleaseEntry(string entry)
        {
            Contract.Requires(entry != null);

            var m = entryRegex.Match(entry);
            if (!m.Success) {
                throw new Exception("Invalid release entry: " + entry);
            }

            if (m.Groups.Count != 4) {
                throw new Exception("Invalid release entry: " + entry);
            }

            long size = Int64.Parse(m.Groups[3].Value);
            bool isDelta = filenameIsDeltaFile(m.Groups[2].Value);
            return new ReleaseEntry(m.Groups[1].Value, m.Groups[2].Value, size, isDelta);
        }

        public static IEnumerable<ReleaseEntry> ParseReleaseFile(string fileContents)
        {
            Contract.Requires(!String.IsNullOrEmpty(fileContents));

            var ret = fileContents.Split('\n')
                .Where(x => !String.IsNullOrWhiteSpace(x))
                .Select(ParseReleaseEntry)
                .ToArray();

            return ret.Any(x => x == null) ? null : ret;
        }

        public static void WriteReleaseFile(IEnumerable<ReleaseEntry> releaseEntries, Stream stream)
        {
            Contract.Requires(releaseEntries != null && releaseEntries.Any());
            Contract.Requires(stream != null);

            using (var sw = new StreamWriter(stream, Encoding.UTF8)) {
                sw.Write(String.Join("\n", releaseEntries.Select(x => x.EntryAsString)));
            }
        }

        public static void WriteReleaseFile(IEnumerable<ReleaseEntry> releaseEntries, string path)
        {
            Contract.Requires(releaseEntries != null && releaseEntries.Any());
            Contract.Requires(!String.IsNullOrEmpty(path));

            using (var f = File.OpenWrite(path)) {
                WriteReleaseFile(releaseEntries, f);
            }
        }

        public static ReleaseEntry GenerateFromFile(Stream file, string filename)
        {
            Contract.Requires(file != null && file.CanRead);
            Contract.Requires(!String.IsNullOrEmpty(filename));

            var hash = Utility.CalculateStreamSHA1(file); 
            return new ReleaseEntry(hash, filename, file.Length, filenameIsDeltaFile(filename));
        }

        public static ReleaseEntry GenerateFromFile(string path)
        {
            using (var inf = File.OpenRead(path)) {
                return GenerateFromFile(inf, Path.GetFileName(path));
            }
        }

        static bool filenameIsDeltaFile(string filename)
        {
            return filename.EndsWith("-delta.nupkg", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}