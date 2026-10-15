using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CraTrail.Core.Evaluation;
using CraTrail.Core.Json;
using CraTrail.Core.Pack;
using CraTrail.Core.Product;

namespace CraTrail.Cli.Commands;

internal sealed record PackOptions(
    string Config,
    string ProductSbom,
    IReadOnlyList<string> Sboms,
    IReadOnlyList<string> Vex,
    string? Grype,
    IReadOnlyList<string> SastSarif,
    string? SecretsSarif,
    string? CvdPolicy,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Tools,
    string? ProductVersion,
    string? SigningMethod,
    string? SignerWorkflow,
    string? Rules,
    string? CreatedAt,
    string Out);

internal static class PackCommand
{
    public static Command Create(CliIO io)
    {
        var config = new Option<string>("--config") { Description = "Path to cra.yml.", DefaultValueFactory = _ => "cra.yml" };
        var productSbom = new Option<string>("--product-sbom") { Description = "CycloneDX JSON SBOM of the whole product.", Required = true };
        var sboms = new Option<string[]>("--sbom") { Description = "More CycloneDX SBOMs, for example backend, frontend and container." };
        var vex = new Option<string[]>("--vex") { Description = "OpenVEX documents." };
        var grype = new Option<string?>("--grype") { Description = "Grype JSON report of the product SBOM." };
        var sast = new Option<string[]>("--sast-sarif") { Description = "SARIF from static analysis." };
        var secrets = new Option<string?>("--secrets-sarif") { Description = "SARIF from the secret scanner." };
        var cvdPolicy = new Option<string?>("--cvd-policy") { Description = "Coordinated vulnerability disclosure policy, usually SECURITY.md." };
        var evidence = new Option<string[]>("--evidence") { Description = "More evidence as kind=path. Kinds: test-results, sbom-quality, nuget-audit, npm-audit-signatures." };
        var tools = new Option<string[]>("--tool") { Description = "A tool used by the pipeline, as name=version." };
        var version = new Option<string?>("--product-version") { Description = "Product version. Defaults to metadata.component.version in the product SBOM." };
        var signingMethod = new Option<string?>("--signing-method") { Description = "How the release is signed, for example github-artifact-attestations." };
        var signerWorkflow = new Option<string?>("--signer-workflow") { Description = "The workflow expected to sign the release." };
        var rules = new Option<string?>("--rules") { Description = "Folder with crosswalk.yaml and profiles. Defaults to the rules shipped with the tool." };
        var createdAt = new Option<string?>("--created-at") { Description = "Creation time in ISO 8601. Defaults to SOURCE_DATE_EPOCH, then the current time." };
        var output = new Option<string>("--out") { Description = "Where to write the evidence pack zip.", Required = true };

        var command = new Command("pack", "Collect SBOMs, scans and VEX, evaluate Annex I and write an evidence pack.")
        {
            config, productSbom, sboms, vex, grype, sast, secrets, cvdPolicy, evidence, tools,
            version, signingMethod, signerWorkflow, rules, createdAt, output,
        };
        command.SetAction(parse => Guard.Run(io, () => Run(io, new PackOptions(
            parse.GetValue(config)!,
            parse.GetValue(productSbom)!,
            parse.GetValue(sboms) ?? [],
            parse.GetValue(vex) ?? [],
            parse.GetValue(grype),
            parse.GetValue(sast) ?? [],
            parse.GetValue(secrets),
            parse.GetValue(cvdPolicy),
            parse.GetValue(evidence) ?? [],
            parse.GetValue(tools) ?? [],
            parse.GetValue(version),
            parse.GetValue(signingMethod),
            parse.GetValue(signerWorkflow),
            parse.GetValue(rules),
            parse.GetValue(createdAt),
            parse.GetValue(output)!))));
        return command;
    }

    internal static int Run(CliIO io, PackOptions options)
    {
        var rules = RulesSource.Load(options.Rules, io);
        var configPath = io.Resolve(options.Config);
        var repositoryRoot = Path.GetDirectoryName(configPath)!;
        var productSbom = Input(io, EvidenceKinds.ProductSbom, options.ProductSbom);
        var version = options.ProductVersion ?? SbomVersion(productSbom)
            ?? throw new ConfigException(["Pass --product-version, or set metadata.component.version in the product SBOM."]);
        var config = CraConfigLoader.Load(File.ReadAllText(configPath), version, rules,
            path => File.Exists(Path.Combine(repositoryRoot, path)));

        var inputs = new List<PackInput> { productSbom };
        inputs.AddRange(options.Sboms.Select(p => Input(io, EvidenceKinds.Sbom, p)));
        inputs.AddRange(options.Vex.Select(p => Input(io, EvidenceKinds.Vex, p)));
        if (options.Grype is { } grype)
            inputs.Add(Input(io, EvidenceKinds.Grype, grype));
        inputs.AddRange(options.SastSarif.Select(p => Input(io, EvidenceKinds.SastSarif, p)));
        if (options.SecretsSarif is { } secrets)
            inputs.Add(Input(io, EvidenceKinds.SecretsSarif, secrets));
        if (options.CvdPolicy is { } policy)
            inputs.Add(Input(io, EvidenceKinds.CvdPolicy, policy));
        foreach (var item in options.Evidence)
        {
            var (kind, path) = Pair(item, "--evidence");
            if (!EvidenceKinds.ExtraEvidence.Contains(kind))
                throw new ConfigException([$"--evidence: unknown kind '{kind}'. Use {string.Join(", ", EvidenceKinds.ExtraEvidence)}."]);
            inputs.Add(Input(io, kind, path));
        }
        foreach (var relative in config.AttestedSources)
        {
            inputs.Add(new PackInput(
                EvidenceKinds.AttestedDocument,
                Path.GetFileName(relative),
                File.ReadAllBytes(Path.Combine(repositoryRoot, relative)),
                relative));
        }

        var request = new PackRequest(
            config.Product,
            inputs,
            [.. options.Tools.Select(t => Pair(t, "--tool")).Select(t => new ToolInfo(t.Key, t.Value))],
            Source(io),
            options.SigningMethod is { } method ? new SigningInfo(method, options.SignerWorkflow) : null,
            CreatedAt(options.CreatedAt, io),
            CliApp.ToolVersion());
        var pack = PackBuilder.Build(request, rules, Evaluator.CreateDefault().Evaluate);

        var outPath = io.Resolve(options.Out);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using (var file = File.Create(outPath))
            PackArchive.Write(pack, file);

        var gate = pack.ReadEvaluation()!.Gate;
        io.Out.WriteLine($"wrote {options.Out}: {pack.Manifest.Files.Count} files, manifest sha256 {PackHashing.Sha256Hex(pack.ManifestBytes)}");
        io.Out.WriteLine(gate.Passed ? "gate: passed" : $"gate: blocked by {string.Join(", ", gate.Blocking.Select(b => b.Check))}");
        return ExitCodes.Ok;
    }

    private static PackInput Input(CliIO io, string kind, string path) =>
        new(kind, Path.GetFileName(path), File.ReadAllBytes(io.Resolve(path)));

    private static string? SbomVersion(PackInput sbom)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(sbom.Content);
        }
        catch (JsonException ex)
        {
            throw new PackBuildException($"{sbom.FileName} is not a valid product-sbom input: {ex.Message}");
        }
        return JsonSelect.Select(node, "metadata.component.version")
            .Select(n => n is JsonValue value && value.TryGetValue(out string? text) ? text : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private static SourceInfo? Source(CliIO io)
    {
        var repository = io.GetEnvironmentVariable("GITHUB_REPOSITORY");
        return repository is null
            ? null
            : new SourceInfo(
                repository,
                io.GetEnvironmentVariable("GITHUB_SHA"),
                io.GetEnvironmentVariable("GITHUB_REF"),
                io.GetEnvironmentVariable("GITHUB_WORKFLOW_REF"),
                io.GetEnvironmentVariable("GITHUB_RUN_ID"));
    }

    private static DateTimeOffset CreatedAt(string? value, CliIO io)
    {
        if (value is not null)
        {
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : throw new ConfigException([$"--created-at: '{value}' is not a date and time."]);
        }
        if (long.TryParse(io.GetEnvironmentVariable("SOURCE_DATE_EPOCH"), NumberStyles.None, CultureInfo.InvariantCulture, out var epoch))
            return DateTimeOffset.FromUnixTimeSeconds(epoch);
        return DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static (string Key, string Value) Pair(string text, string option)
    {
        var index = text.IndexOf('=', StringComparison.Ordinal);
        if (index <= 0 || index == text.Length - 1)
            throw new ConfigException([$"{option}: '{text}' must look like name=value."]);
        return (text[..index], text[(index + 1)..]);
    }
}
