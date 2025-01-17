﻿using Kerberos.NET.Asn1;
using Kerberos.NET.Dns;
using Kerberos.NET.Entities;
using Kerberos.NET.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Kerberos.NET
{
    [TestClass]
    public class ClientTransportTests : BaseTest
    {
        [TestMethod]
        public async Task TransportBase()
        {
            var transport = new NoopTransport();

            IAsn1ApplicationEncoder<KrbApReq> encoded = new KrbApReq { };

            var resp = await transport.SendMessage<KrbApReq, KrbAsRep>("sdf", encoded);

            Assert.IsNotNull(resp);
        }

        [TestMethod, ExpectedException(typeof(KerberosTransportException))]
        public async Task TcpClientConnectExceptional()
        {
            var tcp = new NoDnsTcpTransport(new FakeExceptionLoggerFactory());

            await tcp.SendMessage<KrbApReq>("blah.com", new ReadOnlyMemory<byte>());
        }

        private class NoDnsTcpTransport : TcpKerberosTransport
        {
            public NoDnsTcpTransport(ILoggerFactory logger)
            : base(logger)
            { }

            protected override DnsRecord QueryDomain(string lookup)
            {
                return new DnsRecord { Target = "127.0.0.1", Port = 12345 };
            }
        }

        private class NoopTransport : KerberosTransportBase
        {
            public NoopTransport() { }

            public override ProtocolType Protocol => ProtocolType.Unspecified;

            public override Task<T> SendMessage<T>(string domain, ReadOnlyMemory<byte> req, CancellationToken cancellation = default)
            {
                var cached = QueryDomain(domain);

                Assert.IsNotNull(cached);

                var response = new KrbAsRep()
                {
                    CRealm = "sdf",
                    CName = new KrbPrincipalName { Name = new[] { "sdf" }, Type = PrincipalNameType.NT_ENTERPRISE },
                    MessageType = MessageType.KRB_AS_REP,
                    ProtocolVersionNumber = 5,
                    Ticket = new KrbTicket
                    {
                        Realm = "sdfsdf",
                        SName = new KrbPrincipalName { Name = new[] { "sdf" }, Type = PrincipalNameType.NT_ENTERPRISE },
                        EncryptedPart = new KrbEncryptedData
                        {
                            Cipher = new byte[] { 0x0, 0x0 }
                        },
                    },
                    EncPart = new KrbEncryptedData
                    {
                        Cipher = new byte[] { 0x0, 0x0 }
                    }
                }.EncodeApplication();

                return Task.FromResult(Decode<T>(response));
            }

            protected override IEnumerable<DnsRecord> Query(string lookup)
            {
                return new List<DnsRecord> {
                    new DnsRecord
                    {
                        Name = "sdfd",
                        Port = 0,
                        Priority = 1,
                        Target = "sdfsdfsdfsdf",
                        TimeToLive = 11,
                        Type = DnsRecordType.SRV,
                        Weight = 1
                    }
                };
            }
        }
    }
}
