// <copyright file="Variable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from code under the Apache 2 License from https://github.com/dotliquid/dotliquid

namespace DotLiquid
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using DotLiquid.Exceptions;
    using DotLiquid.Util;

    /// <summary>
    /// Holds variables. Variables are only loaded "just in time"
    /// and are not evaluated as part of the render stage
    ///
    /// {{ monkey }}
    /// {{ user.name }}
    ///
    /// Variables can be combined with filters:
    ///
    /// {{ user | link }}.
    /// </summary>
    public class Variable : IRenderable
    {
        private static readonly Regex FilterParserRegex = R.B(R.Q(@"(?:{0}|(?:\s*(?!(?:{0}))(?:{1}|\S+)\s*)+)"), Liquid.FilterSeparator, Liquid.QuotedFragment);
        private static readonly Regex FilterArgRegex = R.B(R.Q(@"(?:{0}|{1})\s*({2})"), Liquid.FilterArgumentSeparator, Liquid.ArgumentSeparator, Liquid.QuotedFragment);
        private static readonly Regex QuotedAssignFragmentRegex = R.B(R.Q(@"\s*({0})(.*)"), Liquid.QuotedAssignFragment);
        private static readonly Regex FilterSeparatorRegex = R.B(R.Q(@"{0}\s*(.*)"), Liquid.FilterSeparator);
        private static readonly Regex FilterNameRegex = R.B(R.Q(@"\s*(\w+)"));

        public List<Filter> Filters { get; set; }

        public string Name { get; set; }

        private readonly string markup;

        public Variable(string markup)
        {
            this.markup = markup;

            this.Name = null;
            this.Filters = new List<Filter>();

            Match match = QuotedAssignFragmentRegex.Match(markup);
            if (match.Success)
            {
                this.Name = match.Groups[1].Value;
                Match filterMatch = FilterSeparatorRegex.Match(match.Groups[2].Value);
                if (filterMatch.Success)
                {
                    foreach (string f in R.Scan(filterMatch.Value, FilterParserRegex))
                    {
                        Match filterNameMatch = FilterNameRegex.Match(f);
                        if (filterNameMatch.Success)
                        {
                            string filterName = filterNameMatch.Groups[1].Value;
                            List<string> filterArgs = R.Scan(f, FilterArgRegex);
                            this.Filters.Add(new Filter(filterName, filterArgs.ToArray()));
                        }
                    }
                }
            }
        }

        public async Task RenderAsync(Context context, TextWriter result)
        {
            static string ToFormattedString(object o, IFormatProvider ifp) => o is IFormattable ifo ? ifo.ToString(null, ifp) : (o?.ToString() ?? string.Empty);

            object output = await this.RenderInternalAsync(context).ConfigureAwait(false);

            if (output is ILiquidizable)
            {
                output = null;
            }

            if (output != null)
            {
                Func<object, object> transformer = Template.GetValueTypeTransformer(output.GetType());

                if (transformer != null)
                {
                    output = transformer(output);
                }

                // treating Strings as IEnumerable, and was joining Chars in loop
                if (output is string outputString)
                {
                }
                else if (output is IEnumerable)
                {
                    outputString = string.Join(string.Empty, ((IEnumerable)output).Cast<object>().Select(o => ToFormattedString(o, result.FormatProvider)).ToArray());
                }
                else if (output is bool)
                {
                    outputString = output.ToString().ToLower();
                }
                else
                {
                    outputString = ToFormattedString(output, result.FormatProvider);
                }

                await result.WriteAsync(outputString).ConfigureAwait(false);
            }
        }

        private Task<object> RenderInternalAsync(Context context)
        {
            if (this.Name == null)
            {
                return Task.FromResult<object>(null);
            }

            object output = context[this.Name];

            foreach (Filter filter in this.Filters.ToList())
            {
                var filterArgs = filter.Arguments.Select(a => context[a]).ToList();
                try
                {
                    filterArgs.Insert(0, output);
                    output = context.Invoke(filter.Name, filterArgs);
                }
                catch (FilterNotFoundException ex)
                {
                    throw new FilterNotFoundException(string.Format(Liquid.ResourceManager.GetString("VariableFilterNotFoundException"), filter.Name, this.markup.Trim()), ex);
                }
            }

            if (output is IValueTypeConvertible valueTypeConvertibleOutput)
            {
                output = valueTypeConvertibleOutput.ConvertToValueType();
            }

            return Task.FromResult(output);
        }

        /// <summary>
        /// Primarily intended for testing.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal Task<object> RenderAsync(Context context)
        {
            return this.RenderInternalAsync(context);
        }

        public class Filter
        {
            public Filter(string name, string[] arguments)
            {
                this.Name = name;
                this.Arguments = arguments;
            }

            public string Name { get; set; }

            public string[] Arguments { get; set; }
        }
    }
}
