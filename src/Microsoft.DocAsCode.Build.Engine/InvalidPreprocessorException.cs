// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.Engine
{
    using Microsoft.DocAsCode.Exceptions;

    public class InvalidPreprocessorException : DocfxException
    {
        public InvalidPreprocessorException(string message) : base(message)
        {
        }
    }
}
