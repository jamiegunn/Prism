namespace Prism.Common.Results;

/// <summary>
/// Represents the outcome of an operation that does not return a value.
/// Contains either a success state or an <see cref="Error"/>.
/// </summary>
public class Result
{
    private readonly Error? _error;

    /// <summary>
    /// Initializes a new successful <see cref="Result"/>.
    /// </summary>
    private Result()
    {
        IsSuccess = true;
        _error = null;
    }

    /// <summary>
    /// Initializes a new failed <see cref="Result"/> with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    private Result(Error error)
    {
        IsSuccess = false;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error associated with a failed result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success() => new();

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(Error error) => new(error);

    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to wrap in a successful result.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the value.</returns>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>
    /// Creates a failed result of the specified type with the given error.
    /// </summary>
    /// <typeparam name="T">The type parameter for the result.</typeparam>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failed <see cref="Result"/>.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Represents the outcome of an operation that returns a value of type <typeparamref name="T"/>.
/// Contains either a success value or an <see cref="Error"/>.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
public class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// Initializes a new successful <see cref="Result{T}"/> with the specified value.
    /// </summary>
    /// <param name="value">The success value.</param>
    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    /// <summary>
    /// Initializes a new failed <see cref="Result{T}"/> with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    private Result(Error error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value of a successful result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result. Check IsSuccess before accessing Value.");

    /// <summary>
    /// Gets the error of a failed result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Pattern matches on the result, executing the appropriate function.
    /// </summary>
    /// <typeparam name="TResult">The return type of the match functions.</typeparam>
    /// <param name="onSuccess">Function to execute on success, receiving the value.</param>
    /// <param name="onFailure">Function to execute on failure, receiving the error.</param>
    /// <returns>The result of the executed function.</returns>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    /// <summary>
    /// Transforms the value of a successful result using the specified mapping function.
    /// If the result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <typeparam name="TNew">The type of the transformed value.</typeparam>
    /// <param name="map">The function to transform the value.</param>
    /// <returns>A new result with the transformed value or the original error.</returns>
    public Result<TNew> Map<TNew>(Func<T, TNew> map) =>
        IsSuccess ? Result<TNew>.Success(map(_value!)) : Result<TNew>.Failure(_error!);

    /// <summary>
    /// Chains a result-producing operation onto a successful result.
    /// If the result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result's value.</typeparam>
    /// <param name="bind">The function that produces a new result from the value.</param>
    /// <returns>The result of the bind function or the original error.</returns>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> bind) =>
        IsSuccess ? bind(_value!) : Result<TNew>.Failure(_error!);

    /// <summary>
    /// Implicitly converts a value of type <typeparamref name="T"/> to a successful result.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failed result.
    /// </summary>
    /// <param name="error">The error to wrap.</param>
    public static implicit operator Result<T>(Error error) => Failure(error);
}
