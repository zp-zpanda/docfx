﻿
using Jint;

namespace Microsoft.DocAsCode.Build.Engine.Tests
{
    using System.Collections.Generic;
    using Microsoft.DocAsCode.Common;

    using Xunit;

    public class JintProcessorHelperTest
    {
        [Trait("Related", "JintProcessor")]
        [Fact]
        public void TestJObjectConvertWithJToken()
        {
            var testData = ConvertToObjectHelper.ConvertStrongTypeToObject(new TestData());
            {
                var engine = new Jint.Engine();
                var jsValue = JintProcessorHelper.ConvertObjectToJsValue(engine, testData);
                Assert.True(jsValue.IsObject());
                dynamic value = jsValue.ToObject();
                Assert.Equal(2, value.ValueA);
                Assert.Equal("ValueB", value.ValueB);
                System.Dynamic.ExpandoObject valueDict = value.ValueDict;
                var dict = (IDictionary<string, object>)valueDict;
                Assert.Equal("Value1", dict["1"]);
                Assert.Equal(2.0, dict["key"]);
                object[] array = value.ValueList;
                Assert.Equal("ValueA", array[0]);
                Assert.Equal("ValueB", array[1]);
            }
        }

        [Trait("Related", "JintProcessor")]
        [Theory]
        [InlineData("string", "string")]
        [InlineData(1, 1.0)]
        [InlineData(true, true)]
        [InlineData('a', "a")]
        public void TestJObjectConvertWithPrimaryType(object input, object expected)
        {
            var engine = new Jint.Engine();
            var jsValue = JintProcessorHelper.ConvertObjectToJsValue(engine, input);
            Assert.Equal(expected, jsValue.ToObject());
        }

        private sealed class TestData
        {
            public int ValueA { get; set; } = 2;

            public string ValueB { get; set; } = "ValueB";

            public Dictionary<object, object> ValueDict { get; set; } = new Dictionary<object, object> { [1] = "Value1", ["key"] = 2 };

            public List<string> ValueList { get; set; } = new List<string> { "ValueA", "ValueB" };
        }
    }
}
