using EmbeddedPostgres.Core.Interfaces;

namespace EmbeddedPostgres.Core.Extensions.Tests
{
    [TestClass()]
    public class ParameterExtensionsTests
    {
        [TestMethod()]
        public void GetBoolParameterTest()
        {
            var parameter = new Dictionary<string, object>();
            parameter.Add("p1", true);
            parameter.Add("p2", false);

            Assert.AreEqual(parameter.GetBoolParameter("p1"), true);
            Assert.AreEqual(parameter.GetBoolParameter("p2"), false);
            Assert.AreEqual(parameter.GetBoolParameter("p3", false), false);
            Assert.AreEqual(parameter.GetBoolParameter("p4", true), true);
        }

        [TestMethod()]
        public void GetIntParameterTest()
        {
            var parameter = new Dictionary<string, object>();
            parameter.Add("p1", 1);
            parameter.Add("p2", 2);

            Assert.AreEqual(parameter.GetIntParameter("p1"), 1);
            Assert.AreEqual(parameter.GetIntParameter("p2"), 2);
            Assert.AreEqual(parameter.GetIntParameter("p3", 1), 1);
            Assert.AreEqual(parameter.GetIntParameter("p4", 2), 2);
        }

        [TestMethod()]
        public void GetEnumParameterTest()
        {
            var parameter = new Dictionary<string, object>();
            parameter.Add("p1", PgShutdownParams.ShutdownMode.Fast);
            parameter.Add("p2", PgShutdownParams.ShutdownMode.Smart);

            Assert.AreEqual(parameter.GetEnumParameter<PgShutdownParams.ShutdownMode>("p1"), PgShutdownParams.ShutdownMode.Fast);
            Assert.AreEqual(parameter.GetEnumParameter<PgShutdownParams.ShutdownMode>("p2"), PgShutdownParams.ShutdownMode.Smart);
            Assert.AreEqual(parameter.GetEnumParameter<PgShutdownParams.ShutdownMode>("p3", PgShutdownParams.ShutdownMode.Fast), PgShutdownParams.ShutdownMode.Fast);
            Assert.AreEqual(parameter.GetEnumParameter<PgShutdownParams.ShutdownMode>("p4", PgShutdownParams.ShutdownMode.Smart), PgShutdownParams.ShutdownMode.Smart);
        }

        [TestMethod()]
        public void GetStringParameterTest()
        {
            var parameter = new Dictionary<string, object>();
            parameter.Add("p1", "1");
            parameter.Add("p2", "2");

            Assert.AreEqual(parameter.GetStringParameter("p1"), "1");
            Assert.AreEqual(parameter.GetStringParameter("p2"), "2");
            Assert.AreEqual(parameter.GetStringParameter("p3", "1"), "1");
            Assert.AreEqual(parameter.GetStringParameter("p4", "2"), "2");
        }

        [TestMethod()]
        public void GetParameterTest()
        {
            var parameter = new Dictionary<string, object>();
            parameter.Add("p1", PgShutdownParams.Fast);
            parameter.Add("p2", PgShutdownParams.Smart);

            Assert.AreEqual(parameter.GetParameter<PgShutdownParams>("p1"), PgShutdownParams.Fast);
            Assert.AreEqual(parameter.GetParameter<PgShutdownParams>("p2"), PgShutdownParams.Smart);
            Assert.AreEqual(parameter.GetParameter<PgShutdownParams>("p3", PgShutdownParams.Fast), PgShutdownParams.Fast);
            Assert.AreEqual(parameter.GetParameter<PgShutdownParams>("p4", PgShutdownParams.Smart), PgShutdownParams.Smart);
        }
    }
}