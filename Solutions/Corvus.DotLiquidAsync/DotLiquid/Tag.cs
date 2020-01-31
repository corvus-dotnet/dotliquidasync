// <copyright file="Tag.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from code under the Apache 2 License from https://github.com/dotliquid/dotliquid

namespace DotLiquid
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a tag in Liquid:
    /// {% cycle 'one', 'two', 'three' %}.
    /// </summary>
    public class Tag : IRenderable
    {
        /// <summary>
        /// Gets or sets list of the nodes composing the tag.
        /// </summary>
        public List<object> NodeList { get; protected set; }

        /// <summary>
        /// Gets name of the tag.
        /// </summary>
        protected string TagName { get; private set; }

        /// <summary>
        /// Gets content of the tag node except the name.
        /// E.g. for {% tablerow n in numbers cols:3%} {{n}} {% endtablerow %}
        /// It is "n in numbers cols:3".
        /// </summary>
        protected string Markup { get; private set; }

        /// <summary>
        /// Only want to allow Tags to be created in inherited classes or tests.
        /// </summary>
        protected internal Tag()
        {
        }

        internal virtual void AssertTagRulesViolation(List<object> rootNodeList)
        {
        }

        /// <summary>
        /// Initializes the tag.
        /// </summary>
        /// <param name="tagName">Name of the parsed tag.</param>
        /// <param name="markup">Markup of the parsed tag.</param>
        /// <param name="tokens">Tokens of the parsed tag.</param>
        public virtual void Initialize(string tagName, string markup, List<string> tokens)
        {
            this.TagName = tagName;
            this.Markup = markup;
            this.Parse(tokens);
        }

        /// <summary>
        /// Parses the tag.
        /// </summary>
        /// <param name="tokens"></param>
        protected virtual void Parse(List<string> tokens)
        {
        }

        /// <summary>
        /// Gets name of the tag, usually the type name in lowercase.
        /// </summary>
        public string Name
        {
            get { return this.GetType().Name.ToLower(); }
        }

        /// <summary>
        /// Renders the tag.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="result"></param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public virtual Task RenderAsync(Context context, TextWriter result)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Primarily intended for testing.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal async Task<string> RenderAsync(Context context)
        {
            using TextWriter result = new StringWriter(context.FormatProvider);
            await this.RenderAsync(context, result).ConfigureAwait(false);
            return result.ToString();
        }
    }
}
