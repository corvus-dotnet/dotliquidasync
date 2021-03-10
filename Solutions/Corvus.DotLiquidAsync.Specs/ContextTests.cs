namespace DotLiquid.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using DotLiquid.Exceptions;
    using NUnit.Framework;

    [TestFixture]
    public class ContextTests
    {
        #region Classes used in tests

        private static class TestFilters
        {
            public static string Hi(string output)
            {
                return output + " hi!";
            }
        }

        private static class TestContextFilters
        {
            public static string Hi(Context context, string output)
            {
                return output + " hi from " + context["name"] + "!";
            }
        }

        private static class GlobalFilters
        {
            public static string Notice(string output)
            {
                return "Global " + output;
            }
        }

        private static class LocalFilters
        {
            public static string Notice(string output)
            {
                return "Local " + output;
            }
        }

        private class HundredCents : ILiquidizable
        {
            public object ToLiquid()
            {
                return 100;
            }
        }

        private class CentsDrop : Drop
        {
            public object Amount
            {
                get { return new HundredCents(); }
            }

            public bool NonZero
            {
                get { return true; }
            }
        }

        private class ContextSensitiveDrop : Drop
        {
            public object Test()
            {
                return this.Context["test"];
            }
        }

        private class Category : Drop
        {
            public string Name { get; set; }

            public Category(string name)
            {
                this.Name = name;
            }

            public override object ToLiquid()
            {
                return new CategoryDrop(this);
            }
        }

        private class CategoryDrop : IContextAware
        {
            public Category Category { get; set; }
            public Context Context { get; set; }

            public CategoryDrop(Category category)
            {
                this.Category = category;
            }
        }

        private class CounterDrop : Drop
        {
            private int _count;

            public int Count()
            {
                return ++this._count;
            }
        }

        private class ArrayLike : ILiquidizable
        {
            private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Required for reflection.")]
            public object Fetch(int index)
            {
                return null;
            }

            public object this[int index]
            {
                get
                {
                    this._counts[index] += 1;
                    return this._counts[index];
                }
            }

            public object ToLiquid()
            {
                return this;
            }
        }

        #endregion

        private Context _context;

        [OneTimeSetUp]
        public void SetUp()
        {
            this._context = new Context(CultureInfo.InvariantCulture);
        }

        [Test]
        public void TestVariables()
        {
            this._context["string"] = "string";
            Assert.AreEqual("string", this._context["string"]);

            this._context["num"] = 5;
            Assert.AreEqual(5, this._context["num"]);

            this._context["decimal"] = 5m;
            Assert.AreEqual(5m, this._context["decimal"]);

            this._context["float"] = 5.0f;
            Assert.AreEqual(5.0f, this._context["float"]);

            this._context["double"] = 5.0;
            Assert.AreEqual(5.0, this._context["double"]);

            this._context["time"] = TimeSpan.FromDays(1);
            Assert.AreEqual(TimeSpan.FromDays(1), this._context["time"]);

            this._context["date"] = DateTime.Today;
            Assert.AreEqual(DateTime.Today, this._context["date"]);

            DateTime now = DateTime.Now;
            this._context["datetime"] = now;
            Assert.AreEqual(now, this._context["datetime"]);

            var offset = new DateTimeOffset(2013, 9, 10, 0, 10, 32, new TimeSpan(1, 0, 0));
            this._context["datetimeoffset"] = offset;
            Assert.AreEqual(offset, this._context["datetimeoffset"]);

            var guid = Guid.NewGuid();
            this._context["guid"] = guid;
            Assert.AreEqual(guid, this._context["guid"]);

            this._context["bool"] = true;
            Assert.AreEqual(true, this._context["bool"]);

            this._context["bool"] = false;
            Assert.AreEqual(false, this._context["bool"]);

            this._context["nil"] = null;
            Assert.AreEqual(null, this._context["nil"]);
            Assert.AreEqual(null, this._context["nil"]);
        }

        [Test]
        public void TestVariablesNotExisting()
        {
            Assert.AreEqual(null, this._context["does_not_exist"]);
        }

        [Test]
        public async Task TestVariableNotFoundErrors()
        {
            var template = Template.Parse("{{ does_not_exist }}");
            string rendered = await template.RenderAsync();

            Assert.AreEqual("", rendered);
            Assert.AreEqual(1, template.Errors.Count);
            Assert.AreEqual(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), "does_not_exist"), template.Errors[0].Message);
        }

        [Test]
        public async Task TestVariableNotFoundFromAnonymousObject()
        {
            var template = Template.Parse("{{ first.test }}{{ second.test }}");
            string rendered = await template.RenderAsync(Hash.FromAnonymousObject(new { second = new { foo = "hi!" } }));

            Assert.AreEqual("", rendered);
            Assert.AreEqual(2, template.Errors.Count);
            Assert.AreEqual(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), "first.test"), template.Errors[0].Message);
            Assert.AreEqual(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), "second.test"), template.Errors[1].Message);
        }

        [Test]
        public void TestVariableNotFoundException()
        {
            Assert.DoesNotThrow(() => Template.Parse("{{ does_not_exist }}").RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
            {
                ErrorsOutputMode = ErrorsOutputMode.Rethrow
            }).GetAwaiter().GetResult());
        }

        [Test]
        public async Task TestVariableNotFoundExceptionIgnoredForIfStatement()
        {
            var template = Template.Parse("{% if does_not_exist %}abc{% endif %}");
            string rendered = await template.RenderAsync();

            Assert.AreEqual("", rendered);
            Assert.AreEqual(0, template.Errors.Count);
        }

        [Test]
        public async Task TestVariableNotFoundExceptionIgnoredForUnlessStatement()
        {
            var template = Template.Parse("{% unless does_not_exist %}abc{% endunless %}");
            string rendered = await template.RenderAsync();

            Assert.AreEqual("abc", rendered);
            Assert.AreEqual(0, template.Errors.Count);
        }

        [Test]
        public void TestScoping()
        {
            Assert.DoesNotThrow(() =>
            {
                this._context.Push(null);
                this._context.Pop();
            });

            Assert.Throws<ContextException>(() => this._context.Pop());

            Assert.Throws<ContextException>(() =>
            {
                this._context.Push(null);
                this._context.Pop();
                this._context.Pop();
            });
        }

        [Test]
        public void TestLengthQuery()
        {
            this._context["numbers"] = new[] { 1, 2, 3, 4 };
            Assert.AreEqual(4, this._context["numbers.size"]);

            this._context["numbers"] = new Dictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 },
                { 4, 4 }
            };
            Assert.AreEqual(4, this._context["numbers.size"]);

            this._context["numbers"] = new Dictionary<object, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 },
                { 4, 4 },
                { "size", 1000 }
            };
            Assert.AreEqual(1000, this._context["numbers.size"]);
        }

        [Test]
        public void TestHyphenatedVariable()
        {
            this._context["oh-my"] = "godz";
            Assert.AreEqual("godz", this._context["oh-my"]);
        }

        [Test]
        public void TestAddFilter()
        {
            var context = new Context(CultureInfo.InvariantCulture);
            context.AddFilters(new[] { typeof(TestFilters) });
            Assert.AreEqual("hi? hi!", context.Invoke("hi", new List<object> { "hi?" }));

            context = new Context(CultureInfo.InvariantCulture);
            Assert.AreEqual("hi?", context.Invoke("hi", new List<object> { "hi?" }));

            context.AddFilters(new[] { typeof(TestFilters) });
            Assert.AreEqual("hi? hi!", context.Invoke("hi", new List<object> { "hi?" }));
        }

        [Test]
        public void TestAddContextFilter()
        {
            var context = new Context(CultureInfo.InvariantCulture);
            context["name"] = "King Kong";

            context.AddFilters(new[] { typeof(TestContextFilters) });
            Assert.AreEqual("hi? hi from King Kong!", context.Invoke("hi", new List<object> { "hi?" }));

            context = new Context(CultureInfo.InvariantCulture);
            Assert.AreEqual("hi?", context.Invoke("hi", new List<object> { "hi?" }));
        }

        [Test]
        public async Task TestOverrideGlobalFilter()
        {
            Template.RegisterFilter(typeof(GlobalFilters));
            Assert.AreEqual("Global test", await Template.Parse("{{'test' | notice }}").RenderAsync());
            Assert.AreEqual("Local test", await Template.Parse("{{'test' | notice }}").RenderAsync(new RenderParameters(CultureInfo.InvariantCulture) { Filters = new[] { typeof(LocalFilters) } }));
        }

        [Test]
        public void TestOnlyIntendedFiltersMakeItThere()
        {
            var context = new Context(CultureInfo.InvariantCulture);
            var methodsBefore = context.Strainer.Methods.Select(mi => mi.Name).ToList();
            context.AddFilters(new[] { typeof(TestFilters) });
            var methodsAfter = context.Strainer.Methods.Select(mi => mi.Name).ToList();
            CollectionAssert.AreEqual(
                methodsBefore.Concat(new[] { "Hi" }).OrderBy(s => s).ToList(),
                methodsAfter.OrderBy(s => s).ToList());
        }

        [Test]
        public void TestAddItemInOuterScope()
        {
            this._context["test"] = "test";
            this._context.Push(new Hash());
            Assert.AreEqual("test", this._context["test"]);
            this._context.Pop();
            Assert.AreEqual("test", this._context["test"]);
        }

        [Test]
        public void TestAddItemInInnerScope()
        {
            this._context.Push(new Hash());
            this._context["test"] = "test";
            Assert.AreEqual("test", this._context["test"]);
            this._context.Pop();
            Assert.AreEqual(null, this._context["test"]);
        }

        [Test]
        public void TestHierarchicalData()
        {
            this._context["hash"] = new { name = "tobi" };
            Assert.AreEqual("tobi", this._context["hash.name"]);
            Assert.AreEqual("tobi", this._context["hash['name']"]);
        }

        [Test]
        public void TestKeywords()
        {
            Assert.AreEqual(true, this._context["true"]);
            Assert.AreEqual(false, this._context["false"]);
        }

        [Test]
        public void TestDigits()
        {
            Assert.AreEqual(100, this._context["100"]);
            Assert.AreEqual(100.00, this._context[string.Format("100{0}00", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)]);
        }

        [Test]
        public void TestStrings()
        {
            Assert.AreEqual("hello!", this._context["'hello!'"]);
            Assert.AreEqual("hello!", this._context["'hello!'"]);
        }

        [Test]
        public void TestMerge()
        {
            this._context.Merge(new Hash { { "test", "test" } });
            Assert.AreEqual("test", this._context["test"]);
            this._context.Merge(new Hash { { "test", "newvalue" }, { "foo", "bar" } });
            Assert.AreEqual("newvalue", this._context["test"]);
            Assert.AreEqual("bar", this._context["foo"]);
        }

        [Test]
        public void TestArrayNotation()
        {
            this._context["test"] = new[] { 1, 2, 3, 4, 5 };

            Assert.AreEqual(1, this._context["test[0]"]);
            Assert.AreEqual(2, this._context["test[1]"]);
            Assert.AreEqual(3, this._context["test[2]"]);
            Assert.AreEqual(4, this._context["test[3]"]);
            Assert.AreEqual(5, this._context["test[4]"]);
        }

        [Test]
        public void TestRecursiveArrayNotation()
        {
            this._context["test"] = new { test = new[] { 1, 2, 3, 4, 5 } };

            Assert.AreEqual(1, this._context["test.test[0]"]);

            this._context["test"] = new[] { new { test = "worked" } };

            Assert.AreEqual("worked", this._context["test[0].test"]);
        }

        [Test]
        public void TestHashToArrayTransition()
        {
            this._context["colors"] = new {
                Blue = new[] { "003366", "336699", "6699CC", "99CCFF" },
                Green = new[] { "003300", "336633", "669966", "99CC99" },
                Yellow = new[] { "CC9900", "FFCC00", "FFFF99", "FFFFCC" },
                Red = new[] { "660000", "993333", "CC6666", "FF9999" }
            };

            Assert.AreEqual("003366", this._context["colors.Blue[0]"]);
            Assert.AreEqual("FF9999", this._context["colors.Red[3]"]);
        }

        [Test]
        public void TestTryFirst()
        {
            this._context["test"] = new[] { 1, 2, 3, 4, 5 };

            Assert.AreEqual(1, this._context["test.first"]);
            Assert.AreEqual(5, this._context["test.last"]);

            this._context["test"] = new { test = new[] { 1, 2, 3, 4, 5 } };

            Assert.AreEqual(1, this._context["test.test.first"]);
            Assert.AreEqual(5, this._context["test.test.last"]);

            this._context["test"] = new[] { 1 };

            Assert.AreEqual(1, this._context["test.first"]);
            Assert.AreEqual(1, this._context["test.last"]);
        }

        [Test]
        public void TestAccessHashesWithHashNotation()
        {
            this._context["products"] = new { count = 5, tags = new[] { "deepsnow", "freestyle" } };
            this._context["product"] = new { variants = new[] { new { title = "draft151cm" }, new { title = "element151cm" } } };

            Assert.AreEqual(5, this._context["products[\"count\"]"]);
            Assert.AreEqual("deepsnow", this._context["products['tags'][0]"]);
            Assert.AreEqual("deepsnow", this._context["products['tags'].first"]);
            Assert.AreEqual("draft151cm", this._context["product['variants'][0][\"title\"]"]);
            Assert.AreEqual("element151cm", this._context["product['variants'][1]['title']"]);
            Assert.AreEqual("draft151cm", this._context["product['variants'][0]['title']"]);
            Assert.AreEqual("element151cm", this._context["product['variants'].last['title']"]);
        }

        [Test]
        public void TestAccessVariableWithHashNotation()
        {
            this._context["foo"] = "baz";
            this._context["bar"] = "foo";

            Assert.AreEqual("baz", this._context["[\"foo\"]"]);
            Assert.AreEqual("baz", this._context["[bar]"]);
        }

        [Test]
        public void TestAccessHashesWithHashAccessVariables()
        {
            this._context["var"] = "tags";
            this._context["nested"] = new { var = "tags" };
            this._context["products"] = new { count = 5, tags = new[] { "deepsnow", "freestyle" } };

            Assert.AreEqual("deepsnow", this._context["products[var].first"]);
            Assert.AreEqual("freestyle", this._context["products[nested.var].last"]);
        }

        [Test]
        public void TestHashNotationOnlyForHashAccess()
        {
            this._context["array"] = new[] { 1, 2, 3, 4, 5 };
            this._context["hash"] = new { first = "Hello" };

            Assert.AreEqual(1, this._context["array.first"]);
            Assert.AreEqual(null, this._context["array['first']"]);
            Assert.AreEqual("Hello", this._context["hash['first']"]);
        }

        [Test]
        public void TestFirstCanAppearInMiddleOfCallchain()
        {
            this._context["product"] = new { variants = new[] { new { title = "draft151cm" }, new { title = "element151cm" } } };

            Assert.AreEqual("draft151cm", this._context["product.variants[0].title"]);
            Assert.AreEqual("element151cm", this._context["product.variants[1].title"]);
            Assert.AreEqual("draft151cm", this._context["product.variants.first.title"]);
            Assert.AreEqual("element151cm", this._context["product.variants.last.title"]);
        }

        [Test]
        public void TestCents()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { cents = new HundredCents() }));
            Assert.AreEqual(100, this._context["cents"]);
        }

        [Test]
        public void TestNestedCents()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { cents = new { amount = new HundredCents() } }));
            Assert.AreEqual(100, this._context["cents.amount"]);

            this._context.Merge(Hash.FromAnonymousObject(new { cents = new { cents = new { amount = new HundredCents() } } }));
            Assert.AreEqual(100, this._context["cents.cents.amount"]);
        }

        [Test]
        public void TestCentsThroughDrop()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { cents = new CentsDrop() }));
            Assert.AreEqual(100, this._context["cents.amount"]);
        }

        [Test]
        public void TestNestedCentsThroughDrop()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { vars = new { cents = new CentsDrop() } }));
            Assert.AreEqual(100, this._context["vars.cents.amount"]);
        }

        [Test]
        public void TestDropMethodsWithQuestionMarks()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { cents = new CentsDrop() }));
            Assert.AreEqual(true, this._context["cents.non_zero"]);
        }

        [Test]
        public void TestContextFromWithinDrop()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { test = "123", vars = new ContextSensitiveDrop() }));
            Assert.AreEqual("123", this._context["vars.test"]);
        }

        [Test]
        public void TestNestedContextFromWithinDrop()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { test = "123", vars = new { local = new ContextSensitiveDrop() } }));
            Assert.AreEqual("123", this._context["vars.local.test"]);
        }

        [Test]
        public void TestRanges()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { test = 5 }));
            CollectionAssert.AreEqual(Enumerable.Range(1, 5), this._context["(1..5)"] as IEnumerable);
            CollectionAssert.AreEqual(Enumerable.Range(1, 5), this._context["(1..test)"] as IEnumerable);
            CollectionAssert.AreEqual(Enumerable.Range(5, 1), this._context["(test..test)"] as IEnumerable);
        }

        [Test]
        public void TestCentsThroughDropNestedly()
        {
            this._context.Merge(Hash.FromAnonymousObject(new { cents = new { cents = new CentsDrop() } }));
            Assert.AreEqual(100, this._context["cents.cents.amount"]);

            this._context.Merge(Hash.FromAnonymousObject(new { cents = new { cents = new { cents = new CentsDrop() } } }));
            Assert.AreEqual(100, this._context["cents.cents.cents.amount"]);
        }

        [Test]
        public void TestDropWithVariableCalledOnlyOnce()
        {
            this._context["counter"] = new CounterDrop();

            Assert.AreEqual(1, this._context["counter.count"]);
            Assert.AreEqual(2, this._context["counter.count"]);
            Assert.AreEqual(3, this._context["counter.count"]);
        }

        [Test]
        public void TestDropWithKeyOnlyCalledOnce()
        {
            this._context["counter"] = new CounterDrop();

            Assert.AreEqual(1, this._context["counter['count']"]);
            Assert.AreEqual(2, this._context["counter['count']"]);
            Assert.AreEqual(3, this._context["counter['count']"]);
        }

        [Test]
        public void TestDictionaryAsVariable()
        {
            this._context["dynamic"] = Hash.FromReadOnlyDictionary(new Dictionary<string, object> { ["lambda"] = "Hello" });

            Assert.AreEqual("Hello", this._context["dynamic.lambda"]);
        }

        [Test]
        public void TestReadOnlyDictionaryAsVariable()
        {
            this._context["dynamic"] = Hash.FromDictionary(new Dictionary<string, object> { ["lambda"] = "Hello" });

            Assert.AreEqual("Hello", this._context["dynamic.lambda"]);
        }

        [Test]
        public void TestNestedDictionaryAsVariable()
        {
            this._context["dynamic"] = Hash.FromDictionary(new Dictionary<string, object> { ["lambda"] = new Dictionary<string, object> { ["name"] = "Hello" } });

            Assert.AreEqual("Hello", this._context["dynamic.lambda.name"]);
        }

        [Test]
        public void TestNestedReadonlyDictionaryAsVariable()
        {
            this._context["dynamic"] = Hash.FromReadOnlyDictionary(new Dictionary<string, object> { ["lambda"] = new Dictionary<string, object> { ["name"] = "Hello" } });

            Assert.AreEqual("Hello", this._context["dynamic.lambda.name"]);
        }

        [Test]
        public void TestDynamicAsVariable()
        {
            dynamic expandoObject = new ExpandoObject();
            expandoObject.lambda = "Hello";
            this._context["dynamic"] = Hash.FromDictionary(expandoObject);

            Assert.AreEqual("Hello", this._context["dynamic.lambda"]);
        }

        [Test]
        public void TestNestedDynamicAsVariable()
        {
            dynamic root = new ExpandoObject();
            root.lambda = new ExpandoObject();
            root.lambda.name = "Hello";
            this._context["dynamic"] = Hash.FromDictionary(root);

            Assert.AreEqual("Hello", this._context["dynamic.lambda.name"]);
        }

        [Test]
        public void TestProcAsVariable()
        {
            this._context["dynamic"] = (Proc)delegate { return "Hello"; };

            Assert.AreEqual("Hello", this._context["dynamic"]);
        }

        [Test]
        public void TestLambdaAsVariable()
        {
            this._context["dynamic"] = (Proc)(c => "Hello");

            Assert.AreEqual("Hello", this._context["dynamic"]);
        }

        [Test]
        public void TestNestedLambdaAsVariable()
        {
            this._context["dynamic"] = Hash.FromAnonymousObject(new { lambda = (Proc)(c => "Hello") });

            Assert.AreEqual("Hello", this._context["dynamic.lambda"]);
        }

        [Test]
        public void TestArrayContainingLambdaAsVariable()
        {
            this._context["dynamic"] = new object[] { 1, 2, (Proc)(c => "Hello"), 4, 5 };

            Assert.AreEqual("Hello", this._context["dynamic[2]"]);
        }

        [Test]
        public void TestLambdaIsCalledOnce()
        {
            int global = 0;
            this._context["callcount"] = (Proc)(c =>
           {
               ++global;
               return global.ToString();
           });

            Assert.AreEqual("1", this._context["callcount"]);
            Assert.AreEqual("1", this._context["callcount"]);
            Assert.AreEqual("1", this._context["callcount"]);
        }

        [Test]
        public void TestNestedLambdaIsCalledOnce()
        {
            int global = 0;
            this._context["callcount"] = Hash.FromAnonymousObject(new {
                lambda = (Proc)(c =>
               {
                   ++global;
                   return global.ToString();
               })
            });

            Assert.AreEqual("1", this._context["callcount.lambda"]);
            Assert.AreEqual("1", this._context["callcount.lambda"]);
            Assert.AreEqual("1", this._context["callcount.lambda"]);
        }

        [Test]
        public void TestLambdaInArrayIsCalledOnce()
        {
            int global = 0;
            this._context["callcount"] = new object[]
            { 1, 2, (Proc) (c =>
            {
                ++global;
                return global.ToString();
            }), 4, 5
            };

            Assert.AreEqual("1", this._context["callcount[2]"]);
            Assert.AreEqual("1", this._context["callcount[2]"]);
            Assert.AreEqual("1", this._context["callcount[2]"]);
        }

        [Test]
        public void TestAccessToContextFromProc()
        {
            this._context.Registers["magic"] = 345392;

            this._context["magic"] = (Proc)(c => this._context.Registers["magic"]);

            Assert.AreEqual(345392, this._context["magic"]);
        }

        [Test]
        public void TestToLiquidAndContextAtFirstLevel()
        {
            this._context["category"] = new Category("foobar");
            Assert.IsInstanceOf<CategoryDrop>(this._context["category"]);
            Assert.AreEqual(this._context, ((CategoryDrop)this._context["category"]).Context);
        }
    }
}
