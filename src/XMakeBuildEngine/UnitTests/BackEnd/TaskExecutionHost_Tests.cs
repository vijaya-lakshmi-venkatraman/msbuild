﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for the task execution host object.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using NUnit.Framework;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// The test class for the TaskExecutionHost
    /// </summary>
    [TestFixture]
    public class TaskExecutionHost_Tests : ITestTaskHost, IBuildEngine2
    {
        /// <summary>
        /// The set of parameters which have been initialized on the task.
        /// </summary>
        private Dictionary<string, object> _parametersSetOnTask;

        /// <summary>
        /// The set of outputs which were read from the task.
        /// </summary>
        private Dictionary<string, object> _outputsReadFromTask;

        /// <summary>
        /// The task execution host
        /// </summary>
        private ITaskExecutionHost _host;

        /// <summary>
        /// The mock logging service
        /// </summary>
        private ILoggingService _loggingService;

        /// <summary>
        /// The mock logger
        /// </summary>
        private MockLogger _logger;

        /// <summary>
        /// Array containing one item, used for ITaskItem tests.
        /// </summary>
        private ITaskItem[] _oneItem;

        /// <summary>
        /// Array containing two items, used for ITaskItem tests.
        /// </summary>
        private ITaskItem[] _twoItems;

        /// <summary>
        /// The bucket which receives outputs.
        /// </summary>
        private ItemBucket _bucket;

        /// <summary>
        /// Unused.
        /// </summary>
        public bool IsRunningMultipleNodes
        {
            get { return false; }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public bool ContinueOnError
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public int LineNumberOfTaskNode
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public int ColumnNumberOfTaskNode
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public string ProjectFileOfTaskNode
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Prepares the environment for the test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            InitializeHost(false);
        }

        /// <summary>
        /// Cleans up after the test
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                ((IDisposable)_host).Dispose();
            }

            _host = null;
        }

        /// <summary>
        /// Validate that setting parameters with only the required parameters works.
        /// </summary>
        [Test]
        public void ValidateNoParameters()
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = new Tuple<string, ElementLocation>("true", ElementLocation.Create("foo.proj"));

            Assert.IsTrue(_host.SetTaskParameters(parameters));
            Assert.AreEqual(1, _parametersSetOnTask.Count);
            Assert.IsTrue(_parametersSetOnTask.ContainsKey("ExecuteReturnParam"));
        }

        /// <summary>
        /// Validate that setting no parameters when a required parameter exists fails and throws an exception.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ValidateNoParameters_MissingRequired()
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            _host.SetTaskParameters(parameters);
        }

        /// <summary>
        /// Validate that setting a non-existant parameter fails, but does not throw an exception.
        /// </summary>
        [Test]
        public void ValidateNonExistantParameter()
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            parameters["NonExistantParam"] = new Tuple<string, ElementLocation>("foo", ElementLocation.Create("foo.proj"));
            Assert.IsFalse(_host.SetTaskParameters(parameters));
        }

        #region Bool Params

        /// <summary>
        /// Validate that setting a bool param works and sets the right value.
        /// </summary>
        [Test]
        public void TestSetBoolParam()
        {
            ValidateTaskParameter("BoolParam", "true", true);
        }

        /// <summary>
        /// Validate that setting a bool param works and sets the right value.
        /// </summary>
        [Test]
        public void TestSetBoolParamFalse()
        {
            ValidateTaskParameter("BoolParam", "false", false);
        }

        /// <summary>
        /// Validate that setting a bool param with an empty value does not cause the parameter to get set.
        /// </summary>
        [Test]
        public void TestSetBoolParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("BoolParam", "");
        }

        /// <summary>
        /// Validate that setting a bool param with a property which evaluates to nothing does not cause the parameter to get set.
        /// </summary>
        [Test]
        public void TestSetBoolParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("BoolParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting a bool param with an item which evaluates to nothing does not cause the parameter to get set.
        /// </summary>       
        [Test]
        public void TestSetBoolParamEmptyItem()
        {
            ValidateTaskParameterNotSet("BoolParam", "@(NonExistantItem)");
        }

        #endregion 

        #region Bool Array Params

        /// <summary>
        /// Validate that setting a bool array with a single true sets the array to one 'true' value.
        /// </summary>
        [Test]
        public void TestSetBoolArrayParamOneItem()
        {
            ValidateTaskParameterArray("BoolArrayParam", "true", new bool[] { true });
        }

        /// <summary>
        /// Validate that setting a bool array with a list of two values sets them appropriately.
        /// </summary>
        [Test]
        public void TestSetBoolArrayParamTwoItems()
        {
            ValidateTaskParameterArray("BoolArrayParam", "false;true", new bool[] { false, true });
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetBoolArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("BoolArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetBoolArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("BoolArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetBoolArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("BoolArrayParam", "@(NonExistantItem)");
        }

        #endregion 

        #region Int Params

        /// <summary>
        /// Validate that setting an int param with a value of 0 causes it to get the correct value.
        /// </summary>
        [Test]
        public void TestSetIntParamZero()
        {
            ValidateTaskParameter("IntParam", "0", 0);
        }

        /// <summary>
        /// Validate that setting an int param with a value of 1 causes it to get the correct value.
        /// </summary>
        [Test]
        public void TestSetIntParamOne()
        {
            ValidateTaskParameter("IntParam", "1", 1);
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetIntParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("IntParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetIntParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("IntParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetIntParamEmptyItem()
        {
            ValidateTaskParameterNotSet("IntParam", "@(NonExistantItem)");
        }

        #endregion

        #region Int Array Params

        /// <summary>
        /// Validate that setting an int array with a single value causes it to get a single value.
        /// </summary>
        [Test]
        public void TestSetIntArrayParamOneItem()
        {
            ValidateTaskParameterArray("IntArrayParam", "0", new int[] { 0 });
        }

        /// <summary>
        /// Validate that setting an int array with a list of values causes it to get the correct values.
        /// </summary>
        [Test]
        public void TestSetIntArrayParamTwoItems()
        {
            SetTaskParameter("IntArrayParam", "1;0");

            Assert.IsTrue(_parametersSetOnTask.ContainsKey("IntArrayParam"));

            Assert.AreEqual(1, ((int[])_parametersSetOnTask["IntArrayParam"])[0]);
            Assert.AreEqual(0, ((int[])_parametersSetOnTask["IntArrayParam"])[1]);
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetIntArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("IntArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetIntArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("IntArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetIntArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("IntArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region String Params

        /// <summary>
        /// Test that setting a string param sets the correct value.
        /// </summary>
        [Test]
        public void TestSetStringParam()
        {
            ValidateTaskParameter("StringParam", "0", "0");
        }

        /// <summary>
        /// Test that setting a string param sets the correct value.
        /// </summary>
        [Test]
        public void TestSetStringParamOne()
        {
            ValidateTaskParameter("StringParam", "1", "1");
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetStringParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("StringParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetStringParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("StringParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetStringParamEmptyItem()
        {
            ValidateTaskParameterNotSet("StringParam", "@(NonExistantItem)");
        }

        #endregion

        #region String Array Params

        /// <summary>
        /// Validate that setting a string array with a single value sets the correct value.
        /// </summary>
        [Test]
        public void TestSetStringArrayParam()
        {
            ValidateTaskParameterArray("StringArrayParam", "0", new string[] { "0" });
        }

        /// <summary>
        /// Validate that setting a string array with a list of two values sets the correct values.
        /// </summary>
        [Test]
        public void TestSetStringArrayParamOne()
        {
            ValidateTaskParameterArray("StringArrayParam", "1;0", new string[] { "1", "0" });
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetStringArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("StringArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetStringArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("StringArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetStringArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("StringArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region ITaskItem Params

        /// <summary>
        /// Validate that setting an item with an item list evaluating to one item sets the value appropriately, including metadata.
        /// </summary>
        [Test]
        public void TestSetItemParamSingle()
        {
            ValidateTaskParameterItem("ItemParam", "@(ItemListContainingOneItem)", _oneItem[0]);
        }

        /// <summary>
        /// Validate that setting an item with an item list evaluating to two items sets the value appropriately, including metadata.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TestSetItemParamDouble()
        {
            ValidateTaskParameterItems("ItemParam", "@(ItemListContainingTwoItems)", _twoItems);
        }

        /// <summary>
        /// Validate that setting an item with a string results in an item with the evaluated include set to the string.
        /// </summary>
        [Test]
        public void TestSetItemParamString()
        {
            ValidateTaskParameterItem("ItemParam", "MyItemName");
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetItemParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("ItemParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetItemParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("ItemParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetItemParamEmptyItem()
        {
            ValidateTaskParameterNotSet("ItemParam", "@(NonExistantItem)");
        }

        #endregion

        #region ITaskItem Array Params

        /// <summary>
        /// Validate that setting an item array using an item list containing one item sets a single item.
        /// </summary>
        [Test]
        public void TestSetItemArrayParamSingle()
        {
            ValidateTaskParameterItems("ItemArrayParam", "@(ItemListContainingOneItem)", _oneItem);
        }

        /// <summary>
        /// Validate that setting an item array using an item list containing two items sets both items.
        /// </summary>
        [Test]
        public void TestSetItemArrayParamDouble()
        {
            ValidateTaskParameterItems("ItemArrayParam", "@(ItemListContainingTwoItems)", _twoItems);
        }

        /// <summary>
        /// Validate that setting an item array with 
        /// </summary>
        [Test]
        public void TestSetItemArrayParamString()
        {
            ValidateTaskParameterItems("ItemArrayParam", "MyItemName");
        }

        /// <summary>
        /// Validate that setting an item array with a list with multiple values creates multiple items.
        /// </summary>
        [Test]
        public void TestSetItemArrayParamTwoStrings()
        {
            ValidateTaskParameterItems("ItemArrayParam", "MyItemName;MyOtherItemName", new string[] { "MyItemName", "MyOtherItemName" });
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetItemArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("ItemArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a parameter which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetItemArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("ItemArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Test]
        public void TestSetItemArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("ItemArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region Execute Tests

        /// <summary>
        /// Tests that successful execution returns true.
        /// </summary>
        [Test]
        public void TestExecuteTrue()
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = new Tuple<string, ElementLocation>("true", ElementLocation.Create("foo.proj"));

            Assert.IsTrue(_host.SetTaskParameters(parameters));

            bool executeValue = _host.Execute();

            Assert.AreEqual(true, executeValue);
        }

        /// <summary>
        /// Tests that unsuccessful execution returns false.
        /// </summary>
        [Test]
        public void TestExecuteFalse()
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = new Tuple<string, ElementLocation>("false", ElementLocation.Create("foo.proj"));

            Assert.IsTrue(_host.SetTaskParameters(parameters));

            bool executeValue = _host.Execute();

            Assert.AreEqual(false, executeValue);
        }

        /// <summary>
        /// Tests that when Execute throws, the exception bubbles up.
        /// </summary>
        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void TestExecuteThrow()
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = new Tuple<string, ElementLocation>("false", ElementLocation.Create("foo.proj"));

            TearDown();
            InitializeHost(true);

            Assert.IsTrue(_host.SetTaskParameters(parameters));

            bool executeValue = _host.Execute();
        }

        #endregion

        #region Bool Outputs

        /// <summary>
        /// Validate that boolean output to an item produces the correct evaluated include.
        /// </summary>
        [Test]
        public void TestOutputBoolToItem()
        {
            SetTaskParameter("BoolParam", "true");
            ValidateOutputItem("BoolOutput", "True");
        }

        /// <summary>
        /// Validate that boolean output to a property produces the correct evaluated value.
        /// </summary>
        [Test]
        public void TestOutputBoolToProperty()
        {
            SetTaskParameter("BoolParam", "true");
            ValidateOutputProperty("BoolOutput", "True");
        }

        /// <summary>
        /// Validate that boolean array output to an item  array produces the correct evaluated includes.
        /// </summary>
        [Test]
        public void TestOutputBoolArrayToItems()
        {
            SetTaskParameter("BoolArrayParam", "false;true");
            ValidateOutputItems("BoolArrayOutput", new string[] { "False", "True" });
        }

        /// <summary>
        /// Validate that boolean array output to an item produces the correct semi-colon-delimited evaluated value.
        /// </summary>
        [Test]
        public void TestOutputBoolArrayToProperty()
        {
            SetTaskParameter("BoolArrayParam", "false;true");
            ValidateOutputProperty("BoolArrayOutput", "False;True");
        }

        #endregion

        #region Int Outputs

        /// <summary>
        /// Validate that an int output to an item produces the correct evaluated include
        /// </summary>
        [Test]
        public void TestOutputIntToItem()
        {
            SetTaskParameter("IntParam", "42");
            ValidateOutputItem("IntOutput", "42");
        }

        /// <summary>
        /// Validate that an int output to an property produces the correct evaluated value.
        /// </summary>
        [Test]
        public void TestOutputIntToProperty()
        {
            SetTaskParameter("IntParam", "42");
            ValidateOutputProperty("IntOutput", "42");
        }

        /// <summary>
        /// Validate that an int array output to an item produces the correct evaluated includes.
        /// </summary>
        [Test]
        public void TestOutputIntArrayToItems()
        {
            SetTaskParameter("IntArrayParam", "42;99");
            ValidateOutputItems("IntArrayOutput", new string[] { "42", "99" });
        }

        /// <summary>
        /// Validate that an int array output to a property produces the correct semi-colon-delimiated evaluated value.
        /// </summary>
        [Test]
        public void TestOutputIntArrayToProperty()
        {
            SetTaskParameter("IntArrayParam", "42;99");
            ValidateOutputProperty("IntArrayOutput", "42;99");
        }

        #endregion

        #region String Outputs

        /// <summary>
        /// Validate that a string output to an item produces the correct evaluated include.
        /// </summary>
        [Test]
        public void TestOutputStringToItem()
        {
            SetTaskParameter("StringParam", "FOO");
            ValidateOutputItem("StringOutput", "FOO");
        }

        /// <summary>
        /// Validate that a string output to a property produces the correct evaluated value.
        /// </summary>
        [Test]
        public void TestOutputStringToProperty()
        {
            SetTaskParameter("StringParam", "FOO");
            ValidateOutputProperty("StringOutput", "FOO");
        }

        /// <summary>
        /// Validate that an empty string output overwrites the property value
        /// </summary>
        [Test]
        public void TestOutputEmptyStringToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("EmptyStringOutput", String.Empty);
        }

        /// <summary>
        /// Validate that an empty string array output overwrites the property value
        /// </summary>
        [Test]
        public void TestOutputEmptyStringArrayToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("EmptyStringArrayOutput", String.Empty);
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Test]
        public void TestOutputNullStringToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullStringOutput", "initialvalue");
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Test]
        public void TestOutputNullITaskItemToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullITaskItemOutput", "initialvalue");
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Test]
        public void TestOutputNullStringArrayToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullStringArrayOutput", "initialvalue");
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Test]
        public void TestOutputNullITaskItemArrayToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullITaskItemArrayOutput", "initialvalue");
        }

        /// <summary>
        /// Validate that a string array output to an item produces the correct evaluated includes.
        /// </summary>
        [Test]
        public void TestOutputStringArrayToItems()
        {
            SetTaskParameter("StringArrayParam", "FOO;bar");
            ValidateOutputItems("StringArrayOutput", new string[] { "FOO", "bar" });
        }

        /// <summary>
        /// Validate that a string array output to a property produces the correct semi-colon-delimited evaluated value.
        /// </summary>
        [Test]
        public void TestOutputStringArrayToProperty()
        {
            SetTaskParameter("StringArrayParam", "FOO;bar");
            ValidateOutputProperty("StringArrayOutput", "FOO;bar");
        }

        #endregion

        #region Item Outputs

        /// <summary>
        /// Validate that an item output to an item replicates the item, with metadata
        /// </summary>
        [Test]
        public void TestOutputItemToItem()
        {
            SetTaskParameter("ItemParam", "@(ItemListContainingOneItem)");
            ValidateOutputItems("ItemOutput", _oneItem);
        }

        /// <summary>
        /// Validate than an item output to a property produces the correct evaluated value.
        /// </summary>
        [Test]
        public void TestOutputItemToProperty()
        {
            SetTaskParameter("ItemParam", "@(ItemListContainingOneItem)");
            ValidateOutputProperty("ItemOutput", _oneItem[0].ItemSpec);
        }

        /// <summary>
        /// Validate that an item array output to an item replicates the items, with metadata.
        /// </summary>
        [Test]
        public void TestOutputItemArrayToItems()
        {
            SetTaskParameter("ItemArrayParam", "@(ItemListContainingTwoItems)");
            ValidateOutputItems("ItemArrayOutput", _twoItems);
        }

        /// <summary>
        /// Validate that an item array output to a property produces the correct semi-colon-demlimited evaluated value.
        /// </summary>
        [Test]
        public void TestOutputItemArrayToProperty()
        {
            SetTaskParameter("ItemArrayParam", "@(ItemListContainingTwoItems)");
            ValidateOutputProperty("ItemArrayOutput", String.Concat(_twoItems[0].ItemSpec, ";", _twoItems[1].ItemSpec));
        }

        #endregion

        #region Other Output Tests

        /// <summary>
        /// Attempts to gather outputs into an item list from an string task parameter that
        /// returns an empty string. This should be a no-op.
        /// </summary>
        [Test]
        public void TestEmptyStringInStringArrayParameterIntoItemList()
        {
            SetTaskParameter("StringArrayParam", "");
            ValidateOutputItems("StringArrayOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs into an item list from an string task parameter that
        /// returns an empty string. This should be a no-op.
        /// </summary>
        [Test]
        public void TestEmptyStringParameterIntoItemList()
        {
            SetTaskParameter("StringParam", "");
            ValidateOutputItems("StringOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs from a null task parameter of type "ITaskItem[]".  This should succeed.
        /// </summary>
        [Test]
        public void TestNullITaskItemArrayParameter()
        {
            ValidateOutputItems("ItemArrayNullOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "ArrayList".  This should fail.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TestArrayListParameter()
        {
            ValidateOutputItems("ArrayListOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs from a non-existant output.  This should fail.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TestNonexistantOutput()
        {
            Assert.IsFalse(_host.GatherTaskOutputs("NonExistantOutput", ElementLocation.Create(".", 1, 1), true, "output"));
        }

        /// <summary>
        /// object[] should not be a supported output type.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TestOutputObjectArrayToProperty()
        {
            ValidateOutputProperty("ObjectArrayOutput", "");
        }

        #endregion

        #region Other Tests

        /// <summary>
        /// Test that cleanup for task clears out the task instance.
        /// </summary>
        [Test]
        public void TestCleanupForTask()
        {
            _host.CleanupForBatch();
            Assert.IsNotNull((_host as TaskExecutionHost)._UNITTESTONLY_TaskFactoryWrapper);
            _host.CleanupForTask();
            Assert.IsNull((_host as TaskExecutionHost)._UNITTESTONLY_TaskFactoryWrapper);
        }

        /// <summary>
        /// Test that a using task which specifies an invalid assembly produces an exception.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TestTaskResolutionFailureWithUsingTask()
        {
            _loggingService = new MockLoggingService();
            TearDown();
            _host = new TaskExecutionHost();
            TargetLoggingContext tlc = new TargetLoggingContext(_loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

            ProjectInstance project = CreateTestProject();
            _host.InitializeForTask
                (
                this,
                tlc,
                project,
                "TaskWithMissingAssembly",
                ElementLocation.Create("none", 1, 1),
                this,
                false,
#if FEATURE_APPDOMAIN
                null,
#endif
                false,
                CancellationToken.None
                );
            _host.FindTask(null);
            _host.InitializeForBatch(new TaskLoggingContext(_loggingService, tlc.BuildEventContext), _bucket, null);
        }

        /// <summary>
        /// Test that specifying a task with no using task logs an error, but does not throw.
        /// </summary>
        [Test]
        public void TestTaskResolutionFailureWithNoUsingTask()
        {
            TearDown();
            _host = new TaskExecutionHost();
            TargetLoggingContext tlc = new TargetLoggingContext(_loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

            ProjectInstance project = CreateTestProject();
            _host.InitializeForTask
                (
                this,
                tlc,
                project,
                "TaskWithNoUsingTask",
                ElementLocation.Create("none", 1, 1),
                this,
                false,
#if FEATURE_APPDOMAIN
                null,
#endif
                false,
                CancellationToken.None
                );

            _host.FindTask(null);
            _host.InitializeForBatch(new TaskLoggingContext(_loggingService, tlc.BuildEventContext), _bucket, null);
            _logger.AssertLogContains("MSB4036");
        }

        #endregion

        #region ITestTaskHost Members

        /// <summary>
        /// Records that a parameter was set on the task.
        /// </summary>
        public void ParameterSet(string parameterName, object valueSet)
        {
            _parametersSetOnTask[parameterName] = valueSet;
        }

        /// <summary>
        /// Records that an output was read from the task.
        /// </summary>
        public void OutputRead(string parameterName, object actualValue)
        {
            _outputsReadFromTask[parameterName] = actualValue;
        }

        #endregion

        #region IBuildEngine2 Members

        /// <summary>
        /// Unused.
        /// </summary>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IBuildEngine Members

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Validation Routines

        /// <summary>
        /// Is the class a task factory
        /// </summary>
        private static bool IsTaskFactoryClass(Type type, object unused)
        {
            return (type.GetTypeInfo().IsClass &&
                !type.GetTypeInfo().IsAbstract &&
#if FEATURE_TYPE_GETINTERFACE
                (type.GetInterface("Microsoft.Build.Framework.ITaskFactory") != null));
#else
                type.GetInterfaces().Any(interfaceType => interfaceType.FullName == "Microsoft.Build.Framework.ITaskFactory"));
#endif
        }

        /// <summary>
        /// Initialize the host object
        /// </summary>
        /// <param name="throwOnExecute">Should the task throw when executed</param>
        private void InitializeHost(bool throwOnExecute)
        {
            _loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1) as ILoggingService;
            _logger = new MockLogger();
            _loggingService.RegisterLogger(_logger);
            _host = new TaskExecutionHost();
            TargetLoggingContext tlc = new TargetLoggingContext(_loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

            // Set up a temporary project and add some items to it.
            ProjectInstance project = CreateTestProject();

            TypeLoader typeLoader = new TypeLoader(new TypeFilter(IsTaskFactoryClass));
#if FEATURE_ASSEMBLY_LOADFROM
            AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(Assembly.GetAssembly(typeof(TaskBuilderTestTask.TaskBuilderTestTaskFactory)).FullName, null);
#else
            AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(typeof(TaskBuilderTestTask.TaskBuilderTestTaskFactory).GetTypeInfo().FullName, null);
#endif
            LoadedType loadedType = new LoadedType(typeof(TaskBuilderTestTask.TaskBuilderTestTaskFactory), loadInfo);

            TaskBuilderTestTask.TaskBuilderTestTaskFactory taskFactory = new TaskBuilderTestTask.TaskBuilderTestTaskFactory();
            taskFactory.ThrowOnExecute = throwOnExecute;
            string taskName = "TaskBuilderTestTask";
            (_host as TaskExecutionHost)._UNITTESTONLY_TaskFactoryWrapper = new TaskFactoryWrapper(taskFactory, loadedType, taskName, null);
            _host.InitializeForTask
                (
                this,
                tlc,
                project,
                taskName,
                ElementLocation.Create("none", 1, 1),
                this,
                false,
#if FEATURE_APPDOMAIN
                null,
#endif
                false,
                CancellationToken.None
                );

            ProjectTaskInstance taskInstance = project.Targets["foo"].Tasks.First();
            TaskLoggingContext talc = tlc.LogTaskBatchStarted(".", taskInstance);

            ItemDictionary<ProjectItemInstance> itemsByName = new ItemDictionary<ProjectItemInstance>();

            ProjectItemInstance item = new ProjectItemInstance(project, "ItemListContainingOneItem", "a.cs", ".");
            item.SetMetadata("Culture", "fr-fr");
            itemsByName.Add(item);
            _oneItem = new ITaskItem[] { new TaskItem(item) };

            item = new ProjectItemInstance(project, "ItemListContainingTwoItems", "b.cs", ".");
            ProjectItemInstance item2 = new ProjectItemInstance(project, "ItemListContainingTwoItems", "c.cs", ".");
            item.SetMetadata("HintPath", "c:\\foo");
            item2.SetMetadata("HintPath", "c:\\bar");
            itemsByName.Add(item);
            itemsByName.Add(item2);
            _twoItems = new ITaskItem[] { new TaskItem(item), new TaskItem(item2) };

            _bucket = new ItemBucket(new string[0], new Dictionary<string, string>(), new Lookup(itemsByName, new PropertyDictionary<ProjectPropertyInstance>(), null), 0);
            _host.FindTask(null);
            _host.InitializeForBatch(talc, _bucket, null);
            _parametersSetOnTask = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _outputsReadFromTask = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItem(string outputName, string value)
        {
            Assert.IsTrue(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.IsTrue(_outputsReadFromTask.ContainsKey(outputName));

            Assert.AreEqual(1, _bucket.Lookup.GetItems("output").Count);
            Assert.AreEqual(value, _bucket.Lookup.GetItems("output").First().EvaluatedInclude);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItem(string outputName, ITaskItem value)
        {
            Assert.IsTrue(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.IsTrue(_outputsReadFromTask.ContainsKey(outputName));

            Assert.AreEqual(1, _bucket.Lookup.GetItems("output").Count);
            Assert.AreEqual(0, TaskItemComparer.Instance.Compare(value, new TaskItem(_bucket.Lookup.GetItems("output").First())));
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItems(string outputName, string[] values)
        {
            Assert.IsTrue(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.IsTrue(_outputsReadFromTask.ContainsKey(outputName));

            Assert.AreEqual(values.Length, _bucket.Lookup.GetItems("output").Count);
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(values[i], _bucket.Lookup.GetItems("output").ElementAt(i).EvaluatedInclude);
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItems(string outputName, ITaskItem[] values)
        {
            Assert.IsTrue(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.IsTrue(_outputsReadFromTask.ContainsKey(outputName));

            Assert.AreEqual(values.Length, _bucket.Lookup.GetItems("output").Count);
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(0, TaskItemComparer.Instance.Compare(values[i], new TaskItem(_bucket.Lookup.GetItems("output").ElementAt(i))));
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputProperty(string outputName, string value)
        {
            Assert.IsTrue(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), false, "output"));
            Assert.IsTrue(_outputsReadFromTask.ContainsKey(outputName));

            Assert.IsNotNull(_bucket.Lookup.GetProperty("output"));
            Assert.AreEqual(value, _bucket.Lookup.GetProperty("output").EvaluatedValue);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameter(string parameterName, string value, object expectedValue)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));
            Assert.AreEqual(expectedValue, _parametersSetOnTask[parameterName]);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItem(string parameterName, string value)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem actualItem = _parametersSetOnTask[parameterName] as ITaskItem;
            Assert.AreEqual(value, actualItem.ItemSpec);
            Assert.AreEqual(BuiltInMetadata.MetadataCount, actualItem.MetadataCount);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItem(string parameterName, string value, ITaskItem expectedItem)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem actualItem = _parametersSetOnTask[parameterName] as ITaskItem;
            Assert.AreEqual(0, TaskItemComparer.Instance.Compare(expectedItem, actualItem));
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItems(string parameterName, string value)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem[] actualItems = _parametersSetOnTask[parameterName] as ITaskItem[];
            Assert.AreEqual(1, actualItems.Length);
            Assert.AreEqual(value, actualItems[0].ItemSpec);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItems(string parameterName, string value, ITaskItem[] expectedItems)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem[] actualItems = _parametersSetOnTask[parameterName] as ITaskItem[];
            Assert.AreEqual(expectedItems.Length, actualItems.Length);

            for (int i = 0; i < expectedItems.Length; i++)
            {
                Assert.AreEqual(0, TaskItemComparer.Instance.Compare(expectedItems[i], actualItems[i]));
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItems(string parameterName, string value, string[] expectedItems)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem[] actualItems = _parametersSetOnTask[parameterName] as ITaskItem[];
            Assert.AreEqual(expectedItems.Length, actualItems.Length);

            for (int i = 0; i < expectedItems.Length; i++)
            {
                Assert.AreEqual(expectedItems[i], actualItems[i].ItemSpec);
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterArray(string parameterName, string value, object expectedValue)
        {
            SetTaskParameter(parameterName, value);

            Assert.IsTrue(_parametersSetOnTask.ContainsKey(parameterName));

            Array expectedArray = expectedValue as Array;
            Array actualArray = _parametersSetOnTask[parameterName] as Array;

            Assert.AreEqual(expectedArray.Length, actualArray.Length);
            for (int i = 0; i < expectedArray.Length; i++)
            {
                Assert.AreEqual(expectedArray.GetValue(i), actualArray.GetValue(i));
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterNotSet(string parameterName, string value)
        {
            SetTaskParameter(parameterName, value);
            Assert.IsFalse(_parametersSetOnTask.ContainsKey(parameterName));
        }

        #endregion

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void SetTaskParameter(string parameterName, string value)
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = GetStandardParametersDictionary(true);
            parameters[parameterName] = new Tuple<string, ElementLocation>(value, ElementLocation.Create("foo.proj"));
            bool success = _host.SetTaskParameters(parameters);
            Assert.IsTrue(success);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private Dictionary<string, Tuple<string, ElementLocation>> GetStandardParametersDictionary(bool returnParam)
        {
            Dictionary<string, Tuple<string, ElementLocation>> parameters = new Dictionary<string, Tuple<string, ElementLocation>>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = new Tuple<string, ElementLocation>(returnParam ? "true" : "false", ElementLocation.Create("foo.proj"));
            return parameters;
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private IElementLocation GetParameterLocation(string name)
        {
            return ElementLocation.Create(".", 1, 1);
        }

        /// <summary>
        /// Creates a test project.
        /// </summary>
        /// <returns>The project.</returns>
        private ProjectInstance CreateTestProject()
        {
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <UsingTask TaskName='TaskWithMissingAssembly' AssemblyName='madeup' />
                    <ItemGroup>
                        <Compile Include='b.cs' />
                        <Compile Include='c.cs' />
                    </ItemGroup>

                    <ItemGroup>
                        <Reference Include='System' />
                    </ItemGroup>

                    <Target Name='Empty' />

                    <Target Name='Skip' Inputs='testProject.proj' Outputs='testProject.proj' />

                    <Target Name='Error' >
                        <ErrorTask1 ContinueOnError='True'/>                    
                        <ErrorTask2 ContinueOnError='False'/>  
                        <ErrorTask3 /> 
                        <OnError ExecuteTargets='Foo'/>                  
                        <OnError ExecuteTargets='Bar'/>                  
                    </Target>

                    <Target Name='Foo' Inputs='foo.cpp' Outputs='foo.o'>
                        <FooTask1/>
                    </Target>

                    <Target Name='Bar'>
                        <BarTask1/>
                    </Target>

                    <Target Name='Baz' DependsOnTargets='Bar'>
                        <BazTask1/>
                        <BazTask2/>
                    </Target>

                    <Target Name='Baz2' DependsOnTargets='Bar;Foo'>
                        <Baz2Task1/>
                        <Baz2Task2/>
                        <Baz2Task3/>
                    </Target>

                    <Target Name='DepSkip' DependsOnTargets='Skip'>
                        <DepSkipTask1/>
                        <DepSkipTask2/>
                        <DepSkipTask3/>
                    </Target>

                    <Target Name='DepError' DependsOnTargets='Foo;Skip;Error'>
                        <DepSkipTask1/>
                        <DepSkipTask2/>
                        <DepSkipTask3/>
                    </Target>

                </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(projectFileContents)));
            return project.CreateProjectInstance();
        }
    }
}
