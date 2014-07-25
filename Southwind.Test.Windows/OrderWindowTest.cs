﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Signum.Engine;
using Signum.Engine.Authorization;
using Signum.Entities;
using Signum.Utilities;
using Signum.Windows.UIAutomation;
using Southwind.Entities;
using Southwind.Test.Environment;

namespace Southwind.Test.Windows
{
    [TestClass]
    public class OrderWindowTest
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            SouthwindEnvironment.Start();
            AuthLogic.GloballyEnabled = false;
        }

        [TestMethod]
        public void OrderWindowsTestExample()
        {
            Lite<OrderDN> lite = null;
            try
            {
                using (MainWindowProxy win = Common.OpenAndLogin("Normal", "Normal"))
                {
                    using (SearchWindowProxy persons = win.SelectQuery(typeof(PersonDN)))
                    {
                        persons.Search();

                        using (NormalWindowProxy<PersonDN> john = persons.ViewElementAt<PersonDN>(1))
                        {
                            using (NormalWindowProxy<OrderDN> order = john.ConstructFrom(OrderOperation.CreateOrderFromCustomer))
                            {
                                order.EntityLine(a => a.Employee).Autocomplete("Advanced");

                                order.ValueLineValue(a => a.ShipName, Guid.NewGuid().ToString());
                                order.EntityCombo(a => a.ShipVia).SelectToString("FedEx");

                                ProductDN sonicProduct = Database.Query<ProductDN>().SingleEx(p => p.ProductName.Contains("Sonic"));

                                order.DetailGrid().AddRow(sonicProduct.ToLite());

                                Assert.AreEqual(sonicProduct.UnitPrice, order.ValueLineValue(a => a.TotalPrice));

                                order.Execute(OrderOperation.SaveNew);

                                lite = order.Lite();

                                Assert.AreEqual(sonicProduct.UnitPrice, order.ValueLineValue(a => a.TotalPrice));
                            }
                        }
                    }

                    using (NormalWindowProxy<OrderDN> order = win.SelectEntity(lite)) 
                    {
                        Assert.AreEqual(lite.InDB(a => a.TotalPrice), order.ValueLineValue(a => a.TotalPrice));
                    }
                }
            }
            finally
            {
                if(lite != null)
                    lite.Delete();
            }
        }
    }
}
