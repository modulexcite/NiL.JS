﻿using System;
using NiL.JS.Core;
using NiL.JS.Core.Modules;

namespace NiL.JS.BaseLibrary
{
    [Prototype(typeof(Error))]
#if !PORTABLE
    [Serializable]
#endif
    public sealed class ReferenceError : Error
    {
        [DoNotEnumerate]
        public ReferenceError(Arguments args)
            : base(args[0].ToString())
        {

        }

        [DoNotEnumerate]
        public ReferenceError()
        {

        }

        [DoNotEnumerate]
        public ReferenceError(string message)
            : base(message)
        {
        }
    }
}
