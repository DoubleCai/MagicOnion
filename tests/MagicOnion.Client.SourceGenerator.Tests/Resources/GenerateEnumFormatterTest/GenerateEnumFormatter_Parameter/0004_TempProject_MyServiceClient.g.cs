﻿// <auto-generated />
#pragma warning disable CS0618 // 'member' is obsolete: 'text'
#pragma warning disable CS0612 // 'member' is obsolete
#pragma warning disable CS8019 // Unnecessary using directive.

namespace TempProject
{
    partial class MagicOnionInitializer
    {
        static partial class MagicOnionGeneratedClient
        {
            [global::MagicOnion.Ignore]
            public class TempProject_MyServiceClient : global::MagicOnion.Client.MagicOnionClientBase<global::TempProject.IMyService>, global::TempProject.IMyService
            {
                class ClientCore
                {
                    public global::MagicOnion.Client.Internal.RawMethodInvoker<global::TempProject.MyEnum, global::MessagePack.Nil> GetEnumAsync;
                    public ClientCore(global::MagicOnion.Serialization.IMagicOnionSerializerProvider serializerProvider)
                    {
                        this.GetEnumAsync = global::MagicOnion.Client.Internal.RawMethodInvoker.Create_ValueType_ValueType<global::TempProject.MyEnum, global::MessagePack.Nil>(global::Grpc.Core.MethodType.Unary, "IMyService", "GetEnumAsync", serializerProvider);
                    }
                 }

                readonly ClientCore core;

                public TempProject_MyServiceClient(global::MagicOnion.Client.MagicOnionClientOptions options, global::MagicOnion.Serialization.IMagicOnionSerializerProvider serializerProvider) : base(options)
                {
                    this.core = new ClientCore(serializerProvider);
                }

                private TempProject_MyServiceClient(global::MagicOnion.Client.MagicOnionClientOptions options, ClientCore core) : base(options)
                {
                    this.core = core;
                }

                protected override global::MagicOnion.Client.MagicOnionClientBase<global::TempProject.IMyService> Clone(global::MagicOnion.Client.MagicOnionClientOptions options)
                    => new TempProject_MyServiceClient(options, core);

                public global::MagicOnion.UnaryResult<global::MessagePack.Nil> GetEnumAsync(global::TempProject.MyEnum a)
                    => this.core.GetEnumAsync.InvokeUnary(this, "IMyService/GetEnumAsync", a);
            }
        }
    }
}
