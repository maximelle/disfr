﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace disfr.po
{
    public class YYException : Exception
    {
        public YYException(string message) : base(message) { }
    }
}
