﻿using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace Smartstore.IO
{
    public class NotFoundFile : IFile
    {
        private string _dir;

        public NotFoundFile(string subpath, IFileSystem fs)
        {
            SubPath = subpath;
            FileSystem = fs;
        }

        public IFileSystem FileSystem { get; }
        public bool Exists => false;
        public bool IsDirectory => false;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public long Length => -1;
        public Size Size => Size.Empty;

        public string SubPath { get; }
        public string Directory => _dir ??= SubPath.IsEmpty() ? string.Empty : SubPath.Substring(0, SubPath.Length - Name.Length);
        public string Name => SubPath.IsEmpty() ? string.Empty : Path.GetFileName(SubPath);
        public string NameWithoutExtension => SubPath.IsEmpty() ? string.Empty : Path.GetFileNameWithoutExtension(SubPath);
        public string Extension => SubPath.IsEmpty() ? string.Empty : Path.GetExtension(SubPath);
        public string PhysicalPath => null;

        public bool IsSymbolicLink(out string finalPhysicalPath)
        {
            finalPhysicalPath = null;
            return false;
        }

        public Stream CreateReadStream() => throw new NotSupportedException();
    }
}
