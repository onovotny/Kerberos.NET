﻿using Kerberos.NET.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kerberos.NET.Server
{
    using PreAuthHandlerConstructor = Func<IRealmService, KdcPreAuthenticationHandlerBase>;

    public abstract class KdcMessageHandlerBase
    {
        private readonly ConcurrentDictionary<PaDataType, PreAuthHandlerConstructor> preAuthHandlers =
            new ConcurrentDictionary<PaDataType, PreAuthHandlerConstructor>();

        protected ReadOnlyMemory<byte> Message { get; }

        protected ListenerOptions Options { get; }

        protected IRealmService RealmService { get; private set; }

        protected IDictionary<PaDataType, PreAuthHandlerConstructor> PreAuthHandlers
        {
            get => preAuthHandlers;
        }

        protected abstract MessageType MessageType { get; }

        protected KdcMessageHandlerBase(ReadOnlySequence<byte> message, ListenerOptions options)
        {
            Message = message.First;
            Options = options;
        }

        protected async Task SetRealmContext(string realm)
        {
            RealmService = await Options.RealmLocator(realm);
        }

        private async Task<IKerberosMessage> DecodeMessage(ReadOnlyMemory<byte> message)
        {
            var decoded = DecodeMessageCore(message);

            if (decoded.KerberosProtocolVersionNumber != 5)
            {
                throw new InvalidOperationException($"Message version should be set to v5. Actual: {decoded.KerberosProtocolVersionNumber}");
            }

            if (decoded.KerberosMessageType != MessageType)
            {
                throw new InvalidOperationException($"MessageType should match application class. Actual: {decoded.KerberosMessageType}; Expected: {MessageType}");
            }

            await SetRealmContext(decoded.Realm);

            return decoded;
        }

        protected abstract IKerberosMessage DecodeMessageCore(ReadOnlyMemory<byte> message);

        public virtual async Task<ReadOnlyMemory<byte>> Execute()
        {
            try
            {
                var message = await DecodeMessage(Message);

                return await ExecuteCore(message);
            }
            catch (Exception ex)
            {
                return GenerateGenericError(ex, Options);
            }
        }

        protected abstract Task<ReadOnlyMemory<byte>> ExecuteCore(IKerberosMessage message);

        internal static ReadOnlyMemory<byte> GenerateGenericError(Exception ex, ListenerOptions options)
        {
            return GenerateError(KerberosErrorCode.KRB_ERR_GENERIC, options.IsDebug ? $"[Server] {ex}" : null, options.DefaultRealm, "krbtgt");
        }

        internal static ReadOnlyMemory<byte> GenerateError(KerberosErrorCode code, string error, string realm, string sname)
        {
            var krbErr = new KrbError()
            {
                ErrorCode = code,
                EText = error,
                Realm = realm,
                SName = new KrbPrincipalName
                {
                    Type = PrincipalNameType.NT_SRV_INST,
                    Name = new[] {
                        sname, realm
                    }
                }
            };

            return krbErr.EncodeApplication();
        }

        internal void RegisterPreAuthHandlers(ConcurrentDictionary<PaDataType, PreAuthHandlerConstructor> preAuthHandlers)
        {
            foreach (var handler in preAuthHandlers)
            {
                this.preAuthHandlers[handler.Key] = handler.Value;
            }
        }
    }
}
