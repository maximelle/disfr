using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using disfr.Configuration;
using System.IO;

namespace UnitTestConfiguration
{
    [TestClass]
    public class ConfigServiceTest
    {
        [TestMethod]
        public void LoadConfigTest()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "disfr");

            IConfigService configService = new ConfigService(path);
            Assert.AreEqual(configService.QuickFilter, true);

        }

        [TestMethod]
        public void SaveConfigTest()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "disfr");

            IConfigService configService = new ConfigService(path);
            configService.QuickFilter = false;
            configService.Save();
            Assert.AreEqual(configService.QuickFilter, false);
            configService.QuickFilter = true;
            Assert.AreEqual(configService.QuickFilter, true);
        }
    }
}
