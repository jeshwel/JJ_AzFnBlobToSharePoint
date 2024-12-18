using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Exceptions
{
    public class ProcessException : Exception
    {
        public string Container { get; private set; }
        public string FileName { get; private set; }
        public ProcessException(string Message,string Container, string FileName)
            : base(Message)
        {
            this.Container = Container;
            this.FileName = FileName;
        }
    }
}
