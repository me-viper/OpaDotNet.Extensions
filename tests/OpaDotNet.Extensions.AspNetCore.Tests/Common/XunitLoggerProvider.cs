﻿using System.Globalization;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests.Common;

public class XunitLoggerProvider : ILoggerProvider
{
    // Used to distinguish when multiple apps are running as part of the same test.
    private static int _instanceCount = 0;

    private readonly int _providerInstanceId = Interlocked.Increment(ref _instanceCount);
    private readonly ITestOutputHelper _output;
    private readonly LogLevel _minLevel;
    private readonly DateTimeOffset? _logStart;

    public XunitLoggerProvider(ITestOutputHelper output, LogLevel minLevel = LogLevel.Trace, DateTimeOffset? logStart = null)
    {
        _output = output;
        _minLevel = minLevel;
        _logStart = logStart;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName, _minLevel, _logStart, _providerInstanceId);
    }

    public void Dispose()
    {
    }
}

public class XunitLogger : ILogger
{
    private static readonly string[] NewLineChars = { Environment.NewLine };
    private readonly string _category;
    private readonly LogLevel _minLogLevel;
    private readonly ITestOutputHelper _output;
    private readonly DateTimeOffset? _logStart;
    private readonly int _providerInstanceId;

    public XunitLogger(ITestOutputHelper output, string category, LogLevel minLogLevel, DateTimeOffset? logStart, int providerInstanceId)
    {
        _minLogLevel = minLogLevel;
        _category = category;
        _output = output;
        _logStart = logStart;
        _providerInstanceId = providerInstanceId;
    }

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // Buffer the message into a single string in order to avoid shearing the message when running across multiple threads.
        var messageBuilder = new StringBuilder();

        var timestamp = _logStart.HasValue ? $"{(DateTimeOffset.UtcNow - _logStart.Value).TotalSeconds.ToString("N3", CultureInfo.InvariantCulture)}s" : DateTimeOffset.UtcNow.ToString("s", CultureInfo.InvariantCulture);

        var firstLinePrefix = $"| [{timestamp}] I:{_providerInstanceId} {_category} {logLevel}: ";
        var lines = formatter(state, exception).Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
        messageBuilder.AppendLine(firstLinePrefix + lines.FirstOrDefault() ?? string.Empty);

        var additionalLinePrefix = "|" + new string(' ', firstLinePrefix.Length - 1);

        foreach (var line in lines.Skip(1))
        {
            messageBuilder.AppendLine(additionalLinePrefix + line);
        }

        if (exception != null)
        {
            lines = exception.ToString().Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
            additionalLinePrefix = "| ";

            foreach (var line in lines)
            {
                messageBuilder.AppendLine(additionalLinePrefix + line);
            }
        }

        // Remove the last line-break, because ITestOutputHelper only has WriteLine.
        var message = messageBuilder.ToString();

        if (message.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            message = message.Substring(0, message.Length - Environment.NewLine.Length);
        }

        try
        {
            _output.WriteLine(message);
        }
        catch (Exception)
        {
            // We could fail because we're on a background thread and our captured ITestOutputHelper is
            // busted (if the test "completed" before the background thread fired).
            // So, ignore this. There isn't really anything we can do but hope the
            // caller has additional loggers registered
        }
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLogLevel;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => new NullScope();

    private sealed class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

public static class XunitLoggerFactoryExtensions
{
    public static ILoggingBuilder AddXunit(this ILoggingBuilder builder, ITestOutputHelper output)
    {
        builder.Services.AddSingleton<ILoggerProvider>(new XunitLoggerProvider(output));
        return builder;
    }

    public static ILoggingBuilder AddXunit(this ILoggingBuilder builder, ITestOutputHelper output, LogLevel minLevel)
    {
        builder.Services.AddSingleton<ILoggerProvider>(new XunitLoggerProvider(output, minLevel));
        return builder;
    }

    public static ILoggingBuilder AddXunit(this ILoggingBuilder builder, ITestOutputHelper output, LogLevel minLevel, DateTimeOffset? logStart)
    {
        builder.Services.AddSingleton<ILoggerProvider>(new XunitLoggerProvider(output, minLevel, logStart));
        return builder;
    }

    public static ILoggerFactory AddXunit(this ILoggerFactory loggerFactory, ITestOutputHelper output)
    {
        loggerFactory.AddProvider(new XunitLoggerProvider(output));
        return loggerFactory;
    }

    public static ILoggerFactory AddXunit(this ILoggerFactory loggerFactory, ITestOutputHelper output, LogLevel minLevel)
    {
        loggerFactory.AddProvider(new XunitLoggerProvider(output, minLevel));
        return loggerFactory;
    }

    public static ILoggerFactory AddXunit(this ILoggerFactory loggerFactory, ITestOutputHelper output, LogLevel minLevel, DateTimeOffset? logStart)
    {
        loggerFactory.AddProvider(new XunitLoggerProvider(output, minLevel, logStart));
        return loggerFactory;
    }
}