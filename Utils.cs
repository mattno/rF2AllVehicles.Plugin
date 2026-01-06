using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mattno.Plugins
{

    public sealed class FilePath : IEquatable<FilePath>
    {
        public string FullName
        {
            get; private set;
        }

        public string Name
            => Path.GetFileName(FullName);

        public bool Exists
             => File.Exists(FullName);

        public DirectoryPath Directory
            => new DirectoryPath(Path.GetDirectoryName(FullName));

        public bool DirectoryMissing
            => FullName == Name;

        public FilePath(DirectoryPath directory, string path) : this(Path.Combine(directory.FullName, path)) { }


        public FilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(path));
            }
            if (path.Last() == Path.PathSeparator)
            {
                throw new ArgumentException("File path does not look to like a file", nameof(path));
            }
            if (Path.IsPathRooted(path) && Path.GetPathRoot(path) == path)
            {
                throw new ArgumentException("File seams to be the root - which never is a file", nameof(path));
            }

            FullName = path;
        }

        public override bool Equals(object obj) => Equals(obj as FilePath);

        public bool Equals(FilePath other)
            => other != null &&
               string.Equals(FullName, other.FullName, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => FullName.ToLowerInvariant().GetHashCode();

        public override string ToString() => FullName;

        public static implicit operator string(FilePath path) => path.ToString();
        public static explicit operator FilePath(string path) => new FilePath(path);

        // Optional: helper for interop
        public FileInfo ToFileInfo() => new FileInfo(FullName);

        public string[] Parts()
        {
            if (DirectoryMissing)
            {
                return new string[] { Name };
            }
            else
            {
                var dirParts = Directory.Parts();
                var result = new string[dirParts.Length + 1];
                Array.Copy(dirParts, result, dirParts.Length);
                result[dirParts.Length] = Name;
                return result;
            }
        }

    }


    public sealed class DirectoryPath : IEquatable<DirectoryPath>, IComparable<DirectoryPath>
    {
        public string FullName
        {
            get;
        }

        public string Name => Path.GetFileName(FullName);

        public string Root => Path.GetPathRoot(FullName);

        public bool IsRoot => FullName == Root;

        public DirectoryPath Parent
        {
            get
            {
                var parent = Path.GetDirectoryName(FullName);
                return string.IsNullOrEmpty(parent) ? null : new DirectoryPath(parent);
            }
        }

        public bool Exists => Directory.Exists(FullName);


        public DirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Directory path cannot be null or empty", nameof(path));
            }
            FullName = path;
        }

        public override bool Equals(object obj)
            => Equals(obj as DirectoryPath);

        public bool Equals(DirectoryPath other)
            => other != null
               && string.Equals(FullName, other.FullName, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => StringComparer.OrdinalIgnoreCase.GetHashCode(FullName);

        public int CompareTo(DirectoryPath other)
            => string.Compare(FullName, other?.FullName, StringComparison.OrdinalIgnoreCase);

        public override string ToString()
            => FullName;

        public static implicit operator string(DirectoryPath path)
            => path.FullName;

        public static explicit operator DirectoryPath(string path)
            => new DirectoryPath(path);


        public string[] Parts()
        {
            var parts = new List<string>();

            var current = this;
            while (current != null)
            {
                parts.Add(Root == current.FullName
                    ? Root
                    : current.Name);
                current = current.Parent;
            }
            parts.Reverse();
            return parts.ToArray();
        }

        // helper for interop
        public DirectoryInfo ToDirectoryInfo() => new DirectoryInfo(FullName);
    }


}
