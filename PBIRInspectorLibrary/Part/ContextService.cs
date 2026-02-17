using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    public static class ContextService
    {
        private static readonly ThreadLocal<PartContext> _current = new();

        public static PartContext Current
        {
            get => _current.Value;
            internal set => _current.Value = value;
        }
    }
}
