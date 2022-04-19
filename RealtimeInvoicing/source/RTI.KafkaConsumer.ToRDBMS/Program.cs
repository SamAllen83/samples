using System.Data.Common;

using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.StreamConnectors.Kafka;
using EventStore.StreamConnectors.RDBMS;
using EventStore.StreamConnectors.RDBMS.QueryFormatters;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RTI;
using RTI.KafkaConsumer.ToRDBMS;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(svc => {
        svc.SetMinimumLevel(LogLevel.Debug);
    })
    .ConfigureServices(svc => {
        svc.AddSingleton(sp => {
            var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("EventStore");
            var credentials = new UserCredentials("admin", "changeit");
            var connectionSettings = ConnectionSettings.Create()
                .SetDefaultUserCredentials(credentials);
            var connection = EventStoreConnection.Create(Strings.DatabaseConnections.EventStore, connectionSettings, "Consumers - RDBMS");
            connection.ConnectAsync().Wait();
            log.LogDebug("Connected to EventStore");
            return connection;
        });
        svc.AddTransient<DbConnection>(sp => {
            var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("MsSql");
            var connection = new SqlConnection(Strings.DatabaseConnections.SqlServer);
            connection.Open();
            log.LogDebug("Connected to SQL Server");
            return connection;
        });



        // sql projections.
        svc.AddHostedService(sp => {
            var options = new KafkaSqlConsumerConfigurationOptions {
                Stream = "invoice_header",
                Group = Strings.Kafka.Groups.RDBMSInvoiceHeaders,
                Topic = Strings.Kafka.Topics.RDBMSInvoiceHeaders,
                ConnectionString = Strings.DatabaseConnections.Kafka,
                Continuous = true,
                Table = "Invoices",
                Columns = new ColumnMap[] {
                    new ColumnMap("id", "Id", typeof(Guid), true),
                    new ColumnMap("accountId", "AccountId", typeof(Guid)),
                    new ColumnMap("accountName", "AccountName", typeof(string)),
                    new ColumnMap("paymentTermsId", "PaymentTermsId", typeof(Guid)),
                    new ColumnMap("paymentTermsName", "PaymentTermsName", typeof(string)),
                    new ColumnMap("date", "Date", typeof(DateTime)),
                    new ColumnMap("status", "Status", typeof(string)),
                    new ColumnMap("total", "ItemsTotal", typeof(long)),
                    new ColumnMap("paymentsTotal", "PaymentsTotal", typeof(long)),
                    new ColumnMap("balanceDue", "BalanceDue", typeof(long))
                },
                QueryFormatter = new MicrosoftSqlQueryFormatter(),
                UsesStreamNameAsKey = false
            };
            return new InvoiceHeaderConsumer(sp.GetRequiredService<DbConnection>(), options, sp.GetRequiredService<IEventStoreConnection>(), sp.GetRequiredService<ILoggerFactory>());
        });

        svc.AddHostedService(sp => {
            var options = new KafkaSqlConsumerConfigurationOptions {
                Stream = "invoice_item",
                Group = Strings.Kafka.Groups.RDBMSInvoiceItems,
                Topic = Strings.Kafka.Topics.RDBMSInvoiceItems,
                ConnectionString = Strings.DatabaseConnections.Kafka,
                Continuous = true,
                Table = "InvoiceItems",
                Columns = new ColumnMap[] {
                    new ColumnMap("lineItemId", "LineItemId", typeof(Guid), true),
                    new ColumnMap("id", "InvoiceId", typeof(Guid)),
                    new ColumnMap("itemId", "ItemId", typeof(Guid)),
                    new ColumnMap("sku", "SKU", typeof(string)),
                    new ColumnMap("description", "Description", typeof(string)),
                    new ColumnMap("cost", "Cost", typeof(long)),
                    new ColumnMap("unitPrice", "UnitPrice", typeof(long)),
                    new ColumnMap("quantity", "QTY", typeof(long)),
                    new ColumnMap("subtotal", "SubTotal", typeof(long)),
                    new ColumnMap("removed", "HasBeenRemoved", typeof(bool))
                },
                QueryFormatter = new MicrosoftSqlQueryFormatter(),
                UsesStreamNameAsKey = false
            };
            return new InvoiceItemsConsumer(sp.GetRequiredService<DbConnection>(), options, sp.GetRequiredService<IEventStoreConnection>(), sp.GetRequiredService<ILoggerFactory>());
        });
        
        svc.AddHostedService(sp => {
            var options = new KafkaSqlConsumerConfigurationOptions {
                Stream = "invoice_pmt",
                Group = Strings.Kafka.Groups.RDBMSInvoicePayments,
                Topic = Strings.Kafka.Topics.RDBMSInvoicePayments,
                ConnectionString = Strings.DatabaseConnections.Kafka,
                Continuous = true,
                Table = "InvoicePayments",
                Columns = new ColumnMap[] {
                    new ColumnMap("paymentId", "PaymentId", typeof(Guid), true),
                    new ColumnMap("id", "InvoiceId", typeof(Guid)),
                    new ColumnMap("amount", "Amount", typeof(long)),
                    new ColumnMap("received", "Received", typeof(DateTime)),
                    new ColumnMap("voided", "Voided", typeof(bool))
                },
                QueryFormatter = new MicrosoftSqlQueryFormatter(),
                UsesStreamNameAsKey = false
            };
            return new InvoicePaymentsConsumer(sp.GetRequiredService<DbConnection>(), options, sp.GetRequiredService<IEventStoreConnection>(), sp.GetRequiredService<ILoggerFactory>());
        });
    })
    .Build();
host.Run();
