using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GestureRecognizer.Camera
{
    public class GestureDetectionEventArgs : EventArgs
    {
        public List<string> Result { get; set; } = [];
    }
}
