using System;
using System.Collections.Generic;

namespace JJ_AzFnBlobToSharePoint
{
    public class FileSystemItem
    {
        //
        // Summary:
        //     Specifies a file system item’s key.
        //
        // Value:
        //     The key.
        public string Key { get; set; }

        //
        // Summary:
        //     Specifies a file system item’s name.
        //
        // Value:
        //     The name.
        public string Name { get; set; }

        //
        // Summary:
        //     Specifies whether a file system item is a directory.
        //
        // Value:
        //     true, if a file system item is a directory; otherwise, false.
        public bool IsDirectory { get; set; }

        //
        // Summary:
        //     Specifies a timestamp that indicates when the file system item was last modified.
        //
        // Value:
        //     A timestamp.
        public DateTime DateModified { get; set; }

        //
        // Summary:
        //     Specifies a file system item’s size, in bytes.
        //
        // Value:
        //     The size.
        public long Size { get; set; }

        //
        // Summary:
        //     Specifies whether a file system item (a directory) has subdirectories.
        //
        // Value:
        //     true, if a file system item has subdirectories; otherwise, false.
        public bool HasSubDirectories { get; set; }

        //
        // Summary:
        //     Specifies an icon (URL) to be used as the file system item’s thumbnail.
        //
        // Value:
        //     The URL.
        public string Thumbnail { get; set; }

        //
        // Summary:
        //     Gets the collection of custom fields bound to a file system item.
        //
        // Value:
        //     A collection of custom fields.
        public IDictionary<string, object> CustomFields { get; }

        //
        // Summary:
        //     Initializes a new instance of the DevExtreme.AspNet.Mvc.FileManagement.FileSystemItem
        //     class.
        public FileSystemItem()
        {
            CustomFields = new Dictionary<string, object>();
        }
    }
}
