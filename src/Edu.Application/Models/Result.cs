
namespace Edu.Application.Models
{
    public class Result
    {
        public bool IsSuccess { get; set; }
        public IEnumerable<string> Errors { get; set; } = Enumerable.Empty<string>();
        public static Result Success() => new() { IsSuccess = true };
        public static Result Fail(params string[] errors) => new() { IsSuccess = false, Errors = errors };
        public static Result Fail(IEnumerable<string> errors) => new() { IsSuccess = false, Errors = errors };
    }

    public class ExternalLoginCallbackResult
    {
        public bool IsSuccess { get; set; }
        public string? RedirectUrl { get; set; }
        public IEnumerable<string>? Errors { get; set; }
    }
}
