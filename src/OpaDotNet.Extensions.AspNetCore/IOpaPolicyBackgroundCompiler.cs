﻿using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaPolicyBackgroundCompiler
{
    OpaEvaluatorFactory Factory { get; }

    IChangeToken OnRecompiled();
}