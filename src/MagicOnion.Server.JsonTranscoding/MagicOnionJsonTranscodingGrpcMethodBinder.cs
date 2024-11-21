using System.Buffers;
using System.Text.Json;
using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using MagicOnion.Internal;
using MagicOnion.Server.Binder;
using MagicOnion.Server.Binder.Internal;
using MessagePack;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;

namespace MagicOnion.Server.JsonTranscoding;

public class MagicOnionJsonTranscodingGrpcMethodBinder<TService>(
    ServiceMethodProviderContext<TService> context,
    IGrpcServiceActivator<TService> serviceActivator,
    MagicOnionJsonTranscodingOptions options,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory
) : IMagicOnionGrpcMethodBinder<TService>
    where TService : class
{
    public void BindUnary<TRequest, TResponse, TRawRequest, TRawResponse>(IMagicOnionUnaryMethod<TService, TRequest, TResponse, TRawRequest, TRawResponse> method) where TRawRequest : class where TRawResponse : class
    {
        var messageSerializer = new SystemTextJsonMessageSerializer(options.JsonSerializerOptions ?? JsonSerializerOptions.Default);

        var grpcMethod = GrpcMethodHelper.CreateMethod<TRequest, TResponse, TRawRequest, TRawResponse>(MethodType.Unary, method.ServiceName, method.MethodName, messageSerializer);

        var handlerBuilder = new MagicOnionGrpcMethodHandler<TService>(enableCurrentContext: false, isReturnExceptionStackTraceInErrorDetail: false, serviceProvider, [], loggerFactory.CreateLogger<MagicOnionGrpcMethodHandler<TService>>());
        var unaryMethodHandler = handlerBuilder.BuildUnaryMethod(method, messageSerializer);

        var routePath = $"/_/{method.ServiceName}/{method.MethodName}";
        var metadata = method.Metadata.Metadata.Append(new MagicOnionJsonTranscodingMetadata(routePath, typeof(TRequest), typeof(TResponse), method)).ToArray();

        context.AddMethod(grpcMethod, RoutePatternFactory.Parse(routePath), metadata, async (context) =>
        {
            var serverCallContext = new MagicOnionJsonTranscodingServerCallContext(method);

            // Grpc.AspNetCore.Server expects that UserState has the key "__HttpContext" and that HttpContext is set to it.
            // https://github.com/grpc/grpc-dotnet/blob/5a58c24efc1d0b7c5ff88e7b0582ea891b90b17f/src/Grpc.AspNetCore.Server/ServerCallContextExtensions.cs#L30
            serverCallContext.UserState["__HttpContext"] = context;
            context.Features.Set<IServerCallContextFeature>(serverCallContext);

            GrpcActivatorHandle<TService> handle = default;
            try
            {
                handle = serviceActivator.Create(context.RequestServices);

                var memStream = new MemoryStream();
                await context.Request.BodyReader.CopyToAsync(memStream);

                // If the request type is `Nil` (parameter-less method), we always ignore the request body.
                TRawRequest request = (typeof(TRequest) == typeof(Nil))
                    ? (TRawRequest)(object)Box.Create(Nil.Default)
                    : grpcMethod.RequestMarshaller.ContextualDeserializer(new DeserializationContextImpl(memStream.ToArray()));

                var response = await unaryMethodHandler(handle.Instance, request, serverCallContext);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;

                grpcMethod.ResponseMarshaller.ContextualSerializer(response, new SerializationContextImpl(context.Response.BodyWriter));
            }
            catch (RpcException ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                var status = ex.Status;
                await context.Response.BodyWriter.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { Code = status.StatusCode, Detail = status.Detail }));
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                var status = new Status(StatusCode.Internal, $"{ex.GetType().FullName}: {ex.Message}");
                await context.Response.BodyWriter.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { Code = status.StatusCode, Detail = status.Detail }));
            }
            finally
            {
                if (handle.Instance != null)
                {
                    await serviceActivator.ReleaseAsync(handle);
                }
            }

        });
    }

    class SerializationContextImpl(IBufferWriter<byte> writer) : SerializationContext
    {
        public override IBufferWriter<byte> GetBufferWriter() => writer;
        public override void Complete() {}
        public override void SetPayloadLength(int payloadLength) => throw new NotSupportedException();
        public override void Complete(byte[] payload) => throw new NotSupportedException();
    }


    class DeserializationContextImpl(ReadOnlyMemory<byte> bytes) : DeserializationContext
    {
        public override int PayloadLength => bytes.Length;
        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence() => new(bytes);
    }

    public void BindClientStreaming<TRequest, TResponse, TRawRequest, TRawResponse>(MagicOnionClientStreamingMethod<TService, TRequest, TResponse, TRawRequest, TRawResponse> method) where TRawRequest : class where TRawResponse : class
    {
        // Ignore (Currently, not supported)
        throw new NotSupportedException("JsonTranscoding does not support ClientStreaming, ServerStreaming and DuplexStreaming.");
    }

    public void BindServerStreaming<TRequest, TResponse, TRawRequest, TRawResponse>(MagicOnionServerStreamingMethod<TService, TRequest, TResponse, TRawRequest, TRawResponse> method) where TRawRequest : class where TRawResponse : class
    {
        // Ignore (Currently, not supported)
        throw new NotSupportedException("JsonTranscoding does not support ClientStreaming, ServerStreaming and DuplexStreaming.");
    }

    public void BindDuplexStreaming<TRequest, TResponse, TRawRequest, TRawResponse>(MagicOnionDuplexStreamingMethod<TService, TRequest, TResponse, TRawRequest, TRawResponse> method) where TRawRequest : class where TRawResponse : class
    {
        // Ignore (Currently, not supported)
        throw new NotSupportedException("JsonTranscoding does not support ClientStreaming, ServerStreaming and DuplexStreaming.");
    }

    public void BindStreamingHub(MagicOnionStreamingHubConnectMethod<TService> method)
    {
        // Ignore (Currently, not supported)
        throw new NotSupportedException("JsonTranscoding does not support ClientStreaming, ServerStreaming and DuplexStreaming.");
    }
}
