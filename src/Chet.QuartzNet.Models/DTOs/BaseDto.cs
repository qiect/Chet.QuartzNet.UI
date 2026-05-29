namespace Chet.QuartzNet.Models.DTOs
{
    /// <summary>
    /// 分页响应DTO
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PagedResponseDto<T>
    {
        /// <summary>
        /// 数据列表
        /// </summary>
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// 总条数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 页码
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 每页条数
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// API响应DTO
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ApiResponseDto<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 数据
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// 成功响应
        /// </summary>
        public static ApiResponseDto<T> SuccessResponse(T data, string message = "操作成功")
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data,
            };
        }

        /// <summary>
        /// 失败响应
        /// </summary>
        public static ApiResponseDto<T> ErrorResponse(string message, string? errorCode = null)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
            };
        }
    }
}
