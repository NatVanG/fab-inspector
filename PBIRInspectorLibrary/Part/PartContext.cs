using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    public class PartContext
    {
        public IPartQuery PartQuery { get; set; }
        public Part Part { get; set; }
    }
}