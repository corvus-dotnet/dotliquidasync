// <copyright file="Condition.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from code under the Apache 2 License from https://github.com/dotliquid/dotliquid

namespace DotLiquid
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using DotLiquid.Util;

    /// <summary>
    /// Container for liquid nodes which conveniently wraps decision making logic
    ///
    /// Example:
    ///
    /// c = Condition.new('1', '==', '1')
    /// c.evaluate #=> true.
    /// </summary>
    public class Condition
    {
        public static readonly Dictionary<string, ConditionOperatorDelegate> Operators = new Dictionary<string, ConditionOperatorDelegate>()
        {
            { "==", (left, right) => EqualVariables(left, right) },
            { "!=", (left, right) => !EqualVariables(left, right) },
            { "<>", (left, right) => !EqualVariables(left, right) },
            { "<", (left, right) => left != null && right != null && Comparer<object>.Default.Compare(left, Convert.ChangeType(right, left.GetType())) == -1 },
            { ">", (left, right) => left != null && right != null && Comparer<object>.Default.Compare(left, Convert.ChangeType(right, left.GetType())) == 1 },
            { "<=", (left, right) => left != null && right != null && Comparer<object>.Default.Compare(left, Convert.ChangeType(right, left.GetType())) <= 0 },
            { ">=", (left, right) => left != null && right != null && Comparer<object>.Default.Compare(left, Convert.ChangeType(right, left.GetType())) >= 0 },
            { "contains", (left, right) => (left is IList) ? ((IList)left).Contains(right) : ((left is string) ? ((string)left).Contains((string)right) : false) },
            { "startsWith", (left, right) => (left is IList) ? EqualVariables(((IList)left).OfType<object>().FirstOrDefault(), right) : ((left is string) ? ((string)left).StartsWith((string)right) : false) },
            { "endsWith", (left, right) => (left is IList) ? EqualVariables(((IList)left).OfType<object>().LastOrDefault(), right) : ((left is string) ? ((string)left).EndsWith((string)right) : false) },
            { "hasKey", (left, right) => (left is IDictionary) ? ((IDictionary)left).Contains(right) : false },
            { "hasValue", (left, right) => (left is IDictionary) ? ((IDictionary)left).Values.Cast<object>().Contains(right) : false },
        };

        private string childRelation;

        private Condition childCondition;

        public string Left { get; set; }

        public string Operator { get; set; }

        public string Right { get; set; }

        public List<object> Attachment { get; private set; }

        public virtual bool IsElse
        {
            get { return false; }
        }

        public Condition(string left, string @operator, string right)
        {
            this.Left = left;
            this.Operator = @operator;
            this.Right = right;
        }

        public Condition()
        {
        }

        public virtual bool Evaluate(Context context, IFormatProvider formatProvider)
        {
            context ??= new Context(formatProvider);
            bool result = InterpretCondition(this.Left, this.Right, this.Operator, context);

            return this.childRelation switch
            {
                "or" => result || this.childCondition.Evaluate(context, formatProvider),
                "and" => result && this.childCondition.Evaluate(context, formatProvider),
                _ => result,
            };
        }

        public void Or(Condition condition)
        {
            this.childRelation = "or";
            this.childCondition = condition;
        }

        public void And(Condition condition)
        {
            this.childRelation = "and";
            this.childCondition = condition;
        }

        public List<object> Attach(List<object> attachment)
        {
            this.Attachment = attachment;
            return attachment;
        }

        public override string ToString()
        {
            return string.Format("<Condition {0} {1} {2}>", this.Left, this.Operator, this.Right);
        }

        private static bool EqualVariables(object left, object right)
        {
            if (left is Symbol leftSymbol)
            {
                return leftSymbol.EvaluationFunction(right);
            }

            if (right is Symbol rightSymbol)
            {
                return rightSymbol.EvaluationFunction(left);
            }

            if (left != null && right != null && left.GetType() != right.GetType())
            {
                try
                {
                    right = Convert.ChangeType(right, left.GetType());
                }
                catch
                {
                }
            }

            return Equals(left, right);
        }

        private static bool InterpretCondition(string left, string right, string op, Context context)
        {
            // If the operator is empty this means that the decision statement is just
            // a single variable. We can just poll this variable from the context and
            // return this as the result.
            if (string.IsNullOrEmpty(op))
            {
                object result = context[left, false];
                return result != null && (!(result is bool) || (bool)result);
            }

            object leftObject = context[left];
            object rightObject = context[right];

            string opKey = Operators.Keys.FirstOrDefault(opk => opk.Equals(op)
                                                                || opk.ToLowerInvariant().Equals(op)
                                                                || Template.NamingConvention.OperatorEquals(opk, op));
            if (opKey == null)
            {
                throw new Exceptions.ArgumentException(Liquid.ResourceManager.GetString("ConditionUnknownOperatorException"), op);
            }

            return Operators[opKey](leftObject, rightObject);
        }
    }

    public class ElseCondition : Condition
    {
        public override bool IsElse
        {
            get { return true; }
        }

        public override bool Evaluate(Context context, IFormatProvider formatProvider)
        {
            return true;
        }
    }

    public delegate bool ConditionOperatorDelegate(object left, object right);
}
