using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Concrete implementation of <see cref="IBlockRegistry"/> with O(1) interned <see cref="BlockState"/> tables.
/// </summary>
public sealed class BlockRegistry : IBlockRegistry
{
    private static readonly ResourceKey AirKey = ResourceKey.OverHorizon("air");

    private readonly MappedRegistry<BlockDefinition> _blocks = new();
    private readonly List<BlockStateMetadata> _mutableStates = [];
    private readonly Dictionary<(ushort StateId, string PropName, string PropValue), BlockState> _mutableTransitions = [];

    private BlockStateMetadata[] _frozenStates = [];
    private FrozenDictionary<(ushort StateId, string PropName, string PropValue), BlockState>? _frozenTransitions;

    /// <inheritdoc />
    public int Count => _blocks.Count;

    /// <inheritdoc />
    public int StateCount => IsFrozen ? _frozenStates.Length : _mutableStates.Count;

    /// <inheritdoc />
    public bool IsFrozen => _blocks.IsFrozen && _frozenTransitions is not null;

    /// <inheritdoc />
    public IReadOnlyList<BlockDefinition> Entries => _blocks.Entries;

    /// <summary>
    /// Registers a <see cref="BlockDefinition"/> and interns all of its <see cref="BlockState"/> permutations.
    /// </summary>
    /// <param name="definition">Block definition to register.</param>
    /// <returns>Default <see cref="BlockState"/> for the registered block.</returns>
    public BlockState Register(BlockDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (IsFrozen)
        {
            throw new InvalidOperationException($"Cannot register block '{definition.Key}' after registry is frozen.");
        }

        if (_blocks.Count == 0 && definition.Key != AirKey)
        {
            throw new InvalidOperationException($"First registered block (ID 0) must be '{AirKey}', not '{definition.Key}'.");
        }

        var combinations = BuildPropertyCombinations(definition.Properties);
        if (_mutableStates.Count + combinations.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException("Exceeded maximum 65,535 interned BlockState entries.");
        }

        var blockRawId = (ushort)_blocks.Register(definition.Key, definition);
        var assignedStates = ImmutableArray.CreateBuilder<BlockState>(combinations.Count);
        var stateByComboIndex = new BlockState[combinations.Count];

        for (var i = 0; i < combinations.Count; i++)
        {
            var stateId = (ushort)_mutableStates.Count;
            var state = new BlockState(stateId);
            var frozenProps = combinations[i].ToFrozenDictionary(StringComparer.Ordinal);

            _mutableStates.Add(new BlockStateMetadata(state, definition, blockRawId, frozenProps));
            assignedStates.Add(state);
            stateByComboIndex[i] = state;
        }

        definition.States = assignedStates.MoveToImmutable();
        definition.DefaultState = definition.States[0];

        BuildTransitionsForBlock(definition.Properties, combinations, stateByComboIndex);
        return definition.DefaultState;
    }

    /// <summary>
    /// Seals all block and state lookup tables for lock-free concurrent access.
    /// </summary>
    public void Freeze()
    {
        if (IsFrozen)
        {
            return;
        }

        _blocks.Freeze();
        _frozenStates = _mutableStates.ToArray();
        _frozenTransitions = _mutableTransitions.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public BlockDefinition GetById(int rawId) => _blocks.GetById(rawId);

    /// <inheritdoc />
    public bool TryGetById(int rawId, [MaybeNullWhen(false)] out BlockDefinition value) =>
        _blocks.TryGetById(rawId, out value);

    /// <inheritdoc />
    public BlockDefinition GetRequired(ResourceKey key) => _blocks.GetRequired(key);

    /// <inheritdoc />
    public bool TryGet(ResourceKey key, [MaybeNullWhen(false)] out BlockDefinition value) =>
        _blocks.TryGet(key, out value);

    /// <inheritdoc />
    public int GetId(ResourceKey key) => _blocks.GetId(key);

    /// <inheritdoc />
    public ResourceKey GetKey(int rawId) => _blocks.GetKey(rawId);

    /// <inheritdoc />
    public BlockState GetDefaultState(ResourceKey blockKey) => _blocks.GetRequired(blockKey).DefaultState;

    /// <inheritdoc />
    public BlockStateMetadata GetStateMetadata(BlockState state)
    {
        if (IsFrozen)
        {
            var states = _frozenStates;
            if (state.Id < states.Length)
            {
                return states[state.Id];
            }
        }
        else if (state.Id < _mutableStates.Count)
        {
            return _mutableStates[state.Id];
        }

        throw new ArgumentOutOfRangeException(nameof(state), $"Unknown BlockState ID {state.Id}.");
    }

    /// <inheritdoc />
    public BlockState WithProperty(BlockState state, string propertyName, string propertyValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyValue);

        var lookupKey = (state.Id, propertyName, propertyValue);
        if (_frozenTransitions is { } frozen)
        {
            if (frozen.TryGetValue(lookupKey, out var nextState))
            {
                return nextState;
            }
        }
        else if (_mutableTransitions.TryGetValue(lookupKey, out var mutableNext))
        {
            return mutableNext;
        }

        throw new ArgumentException(
            $"Invalid property transition '{propertyName}={propertyValue}' on BlockState {state.Id}.",
            nameof(propertyName));
    }

    private static List<Dictionary<string, string>> BuildPropertyCombinations(ImmutableArray<BlockProperty> properties)
    {
        var results = new List<Dictionary<string, string>> { new(StringComparer.Ordinal) };

        foreach (var prop in properties)
        {
            var expanded = new List<Dictionary<string, string>>(results.Count * prop.AllowedValues.Length);
            foreach (var existing in results)
            {
                foreach (var val in prop.AllowedValues)
                {
                    var copy = new Dictionary<string, string>(existing, StringComparer.Ordinal)
                    {
                        [prop.Name] = val,
                    };
                    expanded.Add(copy);
                }
            }

            results = expanded;
        }

        return results;
    }

    private static int FindMatchingCombination(
        List<Dictionary<string, string>> combinations,
        Dictionary<string, string> baseline,
        string changedProp,
        string changedValue)
    {
        for (var j = 0; j < combinations.Count; j++)
        {
            var candidate = combinations[j];
            var match = true;

            foreach (var (k, v) in candidate)
            {
                var expected = string.Equals(k, changedProp, StringComparison.Ordinal) ? changedValue : baseline[k];
                if (!string.Equals(v, expected, StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return j;
            }
        }

        throw new InvalidOperationException("Unreachable: missing state combination.");
    }

    private void BuildTransitionsForBlock(
        ImmutableArray<BlockProperty> properties,
        List<Dictionary<string, string>> combinations,
        BlockState[] stateByComboIndex)
    {
        if (properties.IsEmpty)
        {
            return;
        }

        for (var i = 0; i < combinations.Count; i++)
        {
            var sourceState = stateByComboIndex[i];
            var sourceProps = combinations[i];

            foreach (var prop in properties)
            {
                foreach (var candidateValue in prop.AllowedValues)
                {
                    var targetIndex = FindMatchingCombination(combinations, sourceProps, prop.Name, candidateValue);
                    _mutableTransitions[(sourceState.Id, prop.Name, candidateValue)] = stateByComboIndex[targetIndex];
                }
            }
        }
    }
}
