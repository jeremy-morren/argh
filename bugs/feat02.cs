// Feature F2 - Dictionary parameters: IReadOnlyDictionary<K,V>, Dictionary<K,V>, ImmutableDictionary<K,V>
// Feature request (see the "Feature requests" section of BugsB.md)
//
// Today a Dictionary<string, int> parameter fails exactly like feat01's types: CS1503 in the generated code,
// no Argh diagnostic (confirmed with IReadOnlyDictionary<string,int>, Dictionary<string,int>, ImmutableDictionary<string,int>).
// Supporting it is a feature, not just recognition:
//   - a CLI syntax: repeated `--label key=value`, and a separator form `--label a=1,b=2`;
//   - key and value parsing for every supported scalar type, including enums and [ArgumentParser] types;
//   - a duplicate-key policy (error, last wins, or merge);
//   - help (`--label <key=value>`), completion of enum keys/values, defaults, nullable, [AsParameters] members;
//   - a __schema shape: cli-schema v1 has no map type, so an array of strings with a pattern, or a vendor extension.
//
// Prior art: pflag StringToString / StringToInt (`--labels a=1,b=2`, repeatable); clap via a custom value parser
// that splits on '='.

namespace Bugs;

internal static class Feat02
{
	// No code: a Dictionary<,> parameter doesn't compile today; feat01 shows the equivalent build break.
}
