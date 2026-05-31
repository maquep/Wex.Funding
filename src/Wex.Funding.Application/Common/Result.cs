namespace Wex.Funding.Application.Common;

public abstract record Result<TSuccess, TError>
{
    public sealed record Ok(TSuccess Value) : Result<TSuccess, TError>;
    public sealed record Err(TError Error) : Result<TSuccess, TError>;

    public bool IsOk => this is Ok;
    public bool IsErr => this is Err;

    public TSuccess Unwrap() => this is Ok ok
        ? ok.Value
        : throw new InvalidOperationException("Result is an error.");

    public TError UnwrapError() => this is Err err
        ? err.Error
        : throw new InvalidOperationException("Result is a success.");
}
