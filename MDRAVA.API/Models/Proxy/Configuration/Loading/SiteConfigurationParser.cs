using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class SiteConfigurationParser
{
    public static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static readonly JsonSerializerOptions WriteJsonOptions = new(ReadJsonOptions)
    {
        WriteIndented = true
    };

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithDuplicateKeyChecking()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    private static readonly ISerializer YamlJsonSerializer = new SerializerBuilder()
        .JsonCompatible()
        .Build();

    public async ValueTask<SiteOptions?> ReadSiteFileAsync(
        string path,
        SiteConfigurationFormat format,
        CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return ReadSiteText(text, format);
    }

    public SiteOptions? ReadSiteText(string text, SiteConfigurationFormat format)
    {
        var json = format == SiteConfigurationFormat.Json
            ? text
            : ConvertYamlToJson(text);

        return JsonSerializer.Deserialize<SiteOptions>(json, ReadJsonOptions);
    }

    public string NormalizeSiteText(string text, SiteConfigurationFormat format)
    {
        var site = ReadSiteText(text, format)
            ?? throw new JsonException("Site configuration did not contain a JSON object.");

        return JsonSerializer.Serialize(site, WriteJsonOptions);
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var yamlObject = YamlDeserializer.Deserialize<object?>(yaml);
        if (yamlObject is null)
        {
            throw new YamlException("YAML did not contain a document.");
        }

        return YamlJsonSerializer.Serialize(yamlObject);
    }
}
