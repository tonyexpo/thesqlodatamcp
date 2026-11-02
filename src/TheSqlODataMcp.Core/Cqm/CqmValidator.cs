using System.Globalization;
using System.Text.Json;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// Catalog-independent structural validation for a constructed <see cref="CqmQuery"/>. It enforces the
/// rules JSON Schema cannot express on its own: alias uniqueness and scope, alias-reference context,
/// operator/function/aggregate arity, aggregate nesting and placement, division by a literal zero, and
/// the analytical/HAVING classification. Catalog resolution, type inference, join resolution, limits, and
/// SQL compilation are deliberately later CQM slices.
/// </summary>
public static class CqmValidator
{
    public static IReadOnlyList<CqmValidationError> Validate(CqmQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new List<CqmValidationError>();
        var aliases = new HashSet<string>(StringComparer.Ordinal) { query.From.Alias };
        var selectAliases = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < query.Select.Count; index++)
        {
            var alias = query.Select[index].Alias;
            if (!selectAliases.Add(alias))
            {
                errors.Add(new CqmValidationError(
                    CqmValidationErrorCodes.DuplicateSelectAlias,
                    string.Create(CultureInfo.InvariantCulture, $"$.select[{index}].alias"),
                    $"The select alias '{alias}' is duplicated."));
            }
        }

        for (var index = 0; index < query.Joins.Count; index++)
        {
            var join = query.Joins[index];
            if (!aliases.Add(join.Source.Alias))
            {
                errors.Add(new CqmValidationError(
                    CqmValidationErrorCodes.DuplicateAlias,
                    string.Create(CultureInfo.InvariantCulture, $"$.joins[{index}].source.alias"),
                    $"The source alias '{join.Source.Alias}' is duplicated."));
            }

            if (join.On is not null)
            {
                var joinPath = string.Create(CultureInfo.InvariantCulture, $"$.joins[{index}].on");
                ValidateJoinCondition(join, join.On, joinPath, errors);
                ValidateExpression(
                    join.On,
                    joinPath,
                    aliases,
                    selectAliases,
                    allowAggregate: false,
                    allowAlias: false,
                    insideAggregate: false,
                    errors);
            }
        }

        if (query.Where is not null)
        {
            ValidateExpression(query.Where, "$.where", aliases, selectAliases, allowAggregate: false, allowAlias: false, insideAggregate: false, errors);
        }

        for (var index = 0; index < query.GroupBy.Count; index++)
        {
            ValidateExpression(
                query.GroupBy[index],
                string.Create(CultureInfo.InvariantCulture, $"$.groupBy[{index}]"),
                aliases,
                selectAliases,
                allowAggregate: false,
                allowAlias: false,
                insideAggregate: false,
                errors);
        }

        for (var index = 0; index < query.Select.Count; index++)
        {
            ValidateExpression(
                query.Select[index].Expression,
                string.Create(CultureInfo.InvariantCulture, $"$.select[{index}].expression"),
                aliases,
                selectAliases,
                allowAggregate: true,
                allowAlias: false,
                insideAggregate: false,
                errors);
        }

        if (query.Having is not null)
        {
            ValidateExpression(query.Having, "$.having", aliases, selectAliases, allowAggregate: true, allowAlias: true, insideAggregate: false, errors);
        }

        for (var index = 0; index < query.OrderBy.Count; index++)
        {
            ValidateExpression(
                query.OrderBy[index].Expression,
                string.Create(CultureInfo.InvariantCulture, $"$.orderBy[{index}].expression"),
                aliases,
                selectAliases,
                allowAggregate: true,
                allowAlias: true,
                insideAggregate: false,
                errors);
        }

        var analytical = query.GroupBy.Count > 0
            || query.Select.Any(static item => ContainsAggregate(item.Expression))
            || (query.Having is not null && ContainsAggregate(query.Having))
            || query.OrderBy.Any(static item => ContainsAggregate(item.Expression));
        if (query.Having is not null && !analytical)
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.HavingWithoutAnalyticalQuery,
                "$.having",
                "having is only valid when the query has grouping or an aggregate."));
        }

        return errors;
    }

    private static void ValidateExpression(
        CqmExpression expression,
        string path,
        HashSet<string> aliases,
        HashSet<string> selectAliases,
        bool allowAggregate,
        bool allowAlias,
        bool insideAggregate,
        List<CqmValidationError> errors)
    {
        switch (expression)
        {
            case CqmFieldExpression field:
                ValidateFieldReference(field.Field, path, aliases, errors);
                break;
            case CqmLiteralExpression literal:
                ValidateLiteral(literal, path, errors);
                break;
            case CqmAliasExpression alias:
                if (!allowAlias)
                {
                    errors.Add(new CqmValidationError(
                        CqmValidationErrorCodes.AliasReferenceNotAllowed,
                        path,
                        $"The alias reference '{alias.Alias}' is not allowed in this position."));
                }
                else if (!selectAliases.Contains(alias.Alias))
                {
                    errors.Add(new CqmValidationError(
                        CqmValidationErrorCodes.AliasReferenceNotFound,
                        path,
                        $"The alias reference '{alias.Alias}' does not resolve to a selected alias."));
                }

                break;
            case CqmOperatorExpression @operator:
                ValidateOperatorArity(@operator, path, errors);
                if (@operator.Operator == CqmOperator.Divide
                    && @operator.Arguments.Count >= 2
                    && IsLiteralZero(@operator.Arguments[1]))
                {
                    errors.Add(new CqmValidationError(
                        CqmValidationErrorCodes.DivisionByZero,
                        string.Concat(path, ".arguments[1]"),
                        "Division by a literal zero is not allowed."));
                }

                for (var index = 0; index < @operator.Arguments.Count; index++)
                {
                    ValidateExpression(
                        @operator.Arguments[index],
                        string.Create(CultureInfo.InvariantCulture, $"{path}.arguments[{index}]"),
                        aliases,
                        selectAliases,
                        allowAggregate,
                        allowAlias,
                        insideAggregate,
                        errors);
                }

                break;
            case CqmFunctionExpression function:
                ValidateFunctionArity(function, path, errors);
                for (var index = 0; index < function.Arguments.Count; index++)
                {
                    ValidateExpression(
                        function.Arguments[index],
                        string.Create(CultureInfo.InvariantCulture, $"{path}.arguments[{index}]"),
                        aliases,
                        selectAliases,
                        allowAggregate,
                        allowAlias,
                        insideAggregate,
                        errors);
                }

                break;
            case CqmAggregateExpression aggregate:
                if (!allowAggregate)
                {
                    errors.Add(new CqmValidationError(
                        CqmValidationErrorCodes.AggregateNotAllowedHere,
                        path,
                        "An aggregate is not allowed in this expression position."));
                }

                if (insideAggregate)
                {
                    errors.Add(new CqmValidationError(
                        CqmValidationErrorCodes.NestedAggregate,
                        path,
                        "Nested aggregates are not supported."));
                }

                if (aggregate.Expression is null)
                {
                    if (aggregate.Aggregate != CqmAggregate.Count || aggregate.Distinct)
                    {
                        errors.Add(new CqmValidationError(
                            CqmValidationErrorCodes.AggregateArityInvalid,
                            path,
                            "This aggregate requires an expression."));
                    }
                }
                else
                {
                    ValidateExpression(
                        aggregate.Expression,
                        string.Concat(path, ".expression"),
                        aliases,
                        selectAliases,
                        allowAggregate: false,
                        allowAlias: false,
                        insideAggregate: true,
                        errors);
                }

                break;
            default:
                throw new InvalidOperationException($"Unsupported CQM expression type '{expression.GetType().Name}'.");
        }
    }

    private static void ValidateJoinCondition(CqmJoin join, CqmExpression on, string path, List<CqmValidationError> errors)
    {
        if (!IsV1JoinCondition(on))
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.JoinConditionInvalid,
                path,
                "A v1 join condition must be eq(field, field) or an and-conjunction of eq(field, field)."));
        }

        if (!ReferencesAlias(on, join.Source.Alias))
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.JoinConditionMissingSourceAlias,
                path,
                $"The join condition must reference the joined source alias '{join.Source.Alias}'."));
        }
    }

    private static bool IsV1JoinCondition(CqmExpression expression) => expression switch
    {
        CqmOperatorExpression { Operator: CqmOperator.Eq } equality
            => equality.Arguments.Count == 2 && equality.Arguments.All(static argument => argument is CqmFieldExpression),
        CqmOperatorExpression { Operator: CqmOperator.And } conjunction
            => conjunction.Arguments.Count >= 2 && conjunction.Arguments.All(IsV1JoinCondition),
        _ => false,
    };

    private static bool ReferencesAlias(CqmExpression expression, string alias) => expression switch
    {
        CqmFieldExpression field => field.Field.StartsWith(string.Concat(alias, "."), StringComparison.Ordinal),
        CqmOperatorExpression @operator => @operator.Arguments.Any(argument => ReferencesAlias(argument, alias)),
        CqmFunctionExpression function => function.Arguments.Any(argument => ReferencesAlias(argument, alias)),
        CqmAggregateExpression aggregate => aggregate.Expression is not null && ReferencesAlias(aggregate.Expression, alias),
        _ => false,
    };

    private static void ValidateFieldReference(string field, string path, HashSet<string> aliases, List<CqmValidationError> errors)
    {
        var separator = field.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == field.Length - 1)
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.FieldReferenceInvalid,
                path,
                $"The field reference '{field}' must use alias.field form."));
            return;
        }

        var alias = field[..separator];
        if (!aliases.Contains(alias))
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.UnknownAlias,
                path,
                $"The field reference '{field}' uses the unknown source alias '{alias}'."));
        }
    }

    private static void ValidateLiteral(CqmLiteralExpression literal, string path, List<CqmValidationError> errors)
    {
        if (literal.Type is not null && !CqmLiteralExpression.IsCompatible(literal.Value, literal.Type.Value))
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.LiteralTypeInvalid,
                string.Concat(path, ".value"),
                $"The literal value is not compatible with the declared canonical type '{literal.Type.Value}'."));
        }
    }

    private static void ValidateOperatorArity(CqmOperatorExpression expression, string path, List<CqmValidationError> errors)
    {
        var (minimum, maximum) = expression.Operator switch
        {
            CqmOperator.Not or CqmOperator.Negate or CqmOperator.IsNull or CqmOperator.IsNotNull => (1, 1),
            CqmOperator.And or CqmOperator.Or => (2, int.MaxValue),
            CqmOperator.In or CqmOperator.NotIn => (2, int.MaxValue),
            CqmOperator.Between => (3, 3),
            _ => (2, 2),
        };

        if (expression.Arguments.Count < minimum || expression.Arguments.Count > maximum)
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.OperatorArityInvalid,
                path,
                $"The operator '{expression.Operator}' requires {DescribeArity(minimum, maximum)} arguments but received {expression.Arguments.Count}."));
        }
    }

    private static void ValidateFunctionArity(CqmFunctionExpression expression, string path, List<CqmValidationError> errors)
    {
        var (minimum, maximum) = expression.Function switch
        {
            CqmFunction.Lower
                or CqmFunction.Upper
                or CqmFunction.Trim
                or CqmFunction.Length
                or CqmFunction.Year
                or CqmFunction.Month
                or CqmFunction.Day
                or CqmFunction.Floor
                or CqmFunction.Ceiling
                or CqmFunction.Absolute => (1, 1),
            CqmFunction.Substring => (2, 3),
            CqmFunction.DateTrunc => (2, 2),
            CqmFunction.Round => (1, 2),
            CqmFunction.Concat or CqmFunction.Coalesce => (2, int.MaxValue),
            _ => (1, int.MaxValue),
        };

        if (expression.Arguments.Count < minimum || expression.Arguments.Count > maximum)
        {
            errors.Add(new CqmValidationError(
                CqmValidationErrorCodes.FunctionArityInvalid,
                path,
                $"The function '{expression.Function}' requires {DescribeArity(minimum, maximum)} arguments but received {expression.Arguments.Count}."));
        }
    }

    private static string DescribeArity(int minimum, int maximum) => maximum switch
    {
        int.MaxValue => $"at least {minimum}",
        _ when minimum == maximum => minimum.ToString(CultureInfo.InvariantCulture),
        _ => $"{minimum} to {maximum}",
    };

    private static bool IsLiteralZero(CqmExpression expression) =>
        expression is CqmLiteralExpression { Value.ValueKind: JsonValueKind.Number } literal
        && CqmLiteralExpression.IsJsonZero(literal.Value);

    private static bool ContainsAggregate(CqmExpression expression) => expression switch
    {
        CqmAggregateExpression => true,
        CqmOperatorExpression @operator => @operator.Arguments.Any(ContainsAggregate),
        CqmFunctionExpression function => function.Arguments.Any(ContainsAggregate),
        _ => false,
    };
}
