﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using Signum.Web;
using Signum.Utilities;
using Southwind.Entities;
using System.Web.Mvc;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Signum.Entities;
using Signum.Entities.Authorization;
using Signum.Engine;
using Signum.Entities.Basics;
using Signum.Entities.SMS;
using Signum.Entities.Mailing;
using Signum.Entities.Files;
using Signum.Web.Files;
using Signum.Web.Operations;
using Southwind.Web.Controllers;
using Signum.Engine.Operations;

namespace Southwind.Web
{
    public static class SouthwindClient
    {
        public static string ViewPrefix = "~/Views/Southwind/{0}.cshtml";
        public static string ThemeSessionKey = "swCurrentTheme";

        public static JsModule OrderModule = new JsModule("Order");
        public static JsModule ProductModule = new JsModule("Product");

        public static void Start()
        {
            if (Navigator.Manager.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                Navigator.AddSettings(new List<EntitySettings>
                {
                    new EmbeddedEntitySettings<AddressDN>() { PartialViewName = e => ViewPrefix.Formato("Address") },

                    new EntitySettings<TerritoryDN>() { PartialViewName = e => ViewPrefix.Formato("Territory") },
                    new EntitySettings<RegionDN>() { PartialViewName = e => ViewPrefix.Formato("Region") },
                    new EntitySettings<EmployeeDN>() { PartialViewName = e => ViewPrefix.Formato("Employee") },

                    new EntitySettings<SupplierDN>() { PartialViewName = e => ViewPrefix.Formato("Supplier") },
                    new EntitySettings<ProductDN>() { PartialViewName = e => ViewPrefix.Formato("Product") },
                    new EntitySettings<CategoryDN>() { PartialViewName = e => ViewPrefix.Formato("Category") },

                    new EntitySettings<PersonDN>() { PartialViewName = e => ViewPrefix.Formato("Person") },
                    new EntitySettings<CompanyDN>() { PartialViewName = e => ViewPrefix.Formato("Company") },
                   
                    new EntitySettings<OrderDN>() { PartialViewName = e => ViewPrefix.Formato("Order") },
                    new EmbeddedEntitySettings<OrderDetailsDN> { PartialViewName = e => ViewPrefix.Formato("OrderDetails") },
                    new EntitySettings<ShipperDN>() { PartialViewName = e => ViewPrefix.Formato("Shipper") },
                    new EntitySettings<ApplicationConfigurationDN>() { PartialViewName = e => ViewPrefix.Formato("ApplicationConfiguration") },
                });

                Constructor.Register(ctx => new ApplicationConfigurationDN { Sms = new SMSConfigurationDN(), Email = new EmailConfigurationDN() });

                QuerySettings.RegisterPropertyFormat((CategoryDN e) => e.Picture,
                    new CellFormatter((html, obj) => obj == null ? null :
                        new HtmlTag("img")
                       .Attr("src", Base64Data((EmbeddedFileDN)obj))
                      .Attr("alt", obj.ToString())
                      .Attr("style", "width:48px").ToHtmlSelf()) { TextAlign = "center" }); // Category

                QuerySettings.RegisterPropertyFormat((EmployeeDN e) => e.Photo,
                    new CellFormatter((html, obj) => obj == null ? null :
                      new HtmlTag("img")
                      .Attr("src", RouteHelper.New().Action((FileController c) => c.Download(new RuntimeInfo((Lite<FileDN>)obj).ToString())))
                      .Attr("alt", obj.ToString())
                      .Attr("style", "width:48px").ToHtmlSelf()) { TextAlign = "center" }); //Emmployee

                Constructor.Register(ctx => new EmployeeDN { Address = new AddressDN() });
                Constructor.Register(ctx => new PersonDN { Address = new AddressDN() });
                Constructor.Register(ctx => new CompanyDN { Address = new AddressDN() });
                Constructor.Register(ctx => new SupplierDN { Address = new AddressDN() });

                OperationClient.AddSettings(new List<OperationSettings>()
                {
                    new ConstructorOperationSettings<OrderDN>(OrderOperation.Create)
                    {
                         ClientConstructor = ctx => OrderModule["createOrder"](ClientConstructorManager.ExtraJsonParams, 
                             new FindOptions(typeof(CustomerDN)){ SearchOnLoad = true }.ToJS(ctx.ClientConstructorContext.Prefix, "cust")),
                         
                         Constructor = ctx=>
                         {
                             var cust = ctx.ConstructorContext.Controller.TryParseLite<CustomerDN>("customer");

                             return OperationLogic.Construct(OrderOperation.Create, cust);
                         }
                    },

                    new ContextualOperationSettings<ProductDN>(OrderOperation.CreateOrderFromProducts)
                    {
                         Click = ctx => OrderModule["createOrderFromProducts"](ctx.Options(), 
                             new FindOptions(typeof(CustomerDN)){ SearchOnLoad = true }.ToJS(ctx.Prefix, "cust"), 
                              ctx.Url.Action((HomeController c)=>c.CreateOrderFromProducts()), 
                             JsFunction.Event)
                    },

                    new EntityOperationSettings<OrderDN>(OrderOperation.SaveNew){ IsVisible = ctx=> ctx.Entity.IsNew }, 
                    new EntityOperationSettings<OrderDN>(OrderOperation.Save){ IsVisible = ctx=> !ctx.Entity.IsNew }, 

                    new EntityOperationSettings<OrderDN>(OrderOperation.Cancel)
                    { 
                        ConfirmMessage = ctx => ((OrderDN)ctx.Entity).State == OrderState.Shipped ? OrderMessage.CancelShippedOrder0.NiceToString(ctx.Entity) : null 
                    }, 

                    new EntityOperationSettings<OrderDN>(OrderOperation.Ship)
                    { 
                        Click = ctx => OrderModule["shipOrder"](ctx.Options(), 
                            ctx.Url.Action((HomeController c)=>c.ShipOrder()), 
                            GetValueLineOptions(ctx.Prefix), 
                            false),

                        Contextual = 
                        { 
                            Click = ctx => OrderModule["shipOrder"](ctx.Options(), 
                                ctx.Url.Action((HomeController c)=>c.ShipOrder()), 
                                GetValueLineOptions(ctx.Prefix), 
                                true),
                        }
                    }, 
                });

                RegisterQuickLinks();
            }
        }

        private static ValueLineBoxOptions GetValueLineOptions(string prefix)
        {
            return new ValueLineBoxOptions(ValueLineType.DateTime, prefix)
            {
                labelText = DescriptionManager.NiceName((OrderDN o) => o.ShippedDate),
                value = DateTime.Now
            };
        }

        private static void RegisterQuickLinks()
        {
            LinksClient.RegisterEntityLinks<UserDN>((entity, ctx) => new[]
                {
                    new QuickLinkExplore(typeof(OperationLogDN), "User", entity)
                });

            LinksClient.RegisterEntityLinks<EmployeeDN>((entity, ctx) =>
            {
                var links = new List<QuickLink>()
                {
                    new QuickLinkExplore(typeof(OrderDN), "Employee", entity)  
                };

                var user = Database.Query<UserDN>()
                    .Where(u => entity.RefersTo(u.Mixin<UserEmployeeMixin>().Employee))
                    .Select(u => u.ToLite())
                    .FirstOrDefault();
                if (user != null)
                    links.Add(new QuickLinkView(user));

                return links.ToArray();
            });

            LinksClient.RegisterEntityLinks<CategoryDN>((entity, ctx) => new[]
            {
                new QuickLinkExplore(typeof(ProductDN), "Category", entity)
            });

            LinksClient.RegisterEntityLinks<SupplierDN>((entity, ctx) => new[]
            {
                new QuickLinkExplore(typeof(ProductDN), "Supplier", entity)
            });

            LinksClient.RegisterEntityLinks<PersonDN>((entity, ctx) => new[]
            {
                new QuickLinkExplore(typeof(OrderDN), "Customer", entity)
            });

            LinksClient.RegisterEntityLinks<CompanyDN>((entity, ctx) => new[]
            {
                new QuickLinkExplore(typeof(OrderDN), "Customer", entity)
            });
        } //RegisterQuickLinks

        public static string Base64Data(EmbeddedFileDN file)
        {
            return "data:" + MimeType.FromFileName(file.FileName) + ";base64," + Convert.ToBase64String(file.BinaryFile);
        } //Base64Data
    }
}