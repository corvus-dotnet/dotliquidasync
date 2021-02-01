// <copyright file="Document.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from code under the Apache 2 License from https://github.com/dotliquid/dotliquid

namespace DotLiquid
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using DotLiquid.Exceptions;

    /// <summary>
    /// Represents the Liquid template.
    /// </summary>
    public class Document : Block
    {
        /// <summary>
        /// We don't need markup to open this block.
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="markup"></param>
        /// <param name="tokens"></param>
        public override void Initialize(string tagName, string markup, List<string> tokens)
        {
            this.Parse(tokens);
        }

        /// <summary>
        /// Gets there isn't a real delimiter.
        /// </summary>
        protected override string BlockDelimiter
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// Document blocks don't need to be terminated since they are not actually opened.
        /// </summary>
        protected override void AssertMissingDelimitation()
        {
        }

        /// <summary>
        /// Renders the Document.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="result"></param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async override Task RenderAsync(Context context, TextWriter result)
        {
            try
            {
                await base.RenderAsync(context, result).ConfigureAwait(false);
            }
            catch (BreakInterrupt)
            {
                // BreakInterrupt exceptions are used to interrupt a rendering
            }
            catch (ContinueInterrupt)
            {
                // ContinueInterrupt exceptions are used to interrupt a rendering
            }
        }
    }
}
