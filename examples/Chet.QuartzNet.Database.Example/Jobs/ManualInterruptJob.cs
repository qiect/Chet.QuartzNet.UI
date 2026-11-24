using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.Database.Example.Jobs
{
    /// <summary>
    /// 手动中断作业（配合日志管理页的「中断」按钮演示）
    ///
    /// Quartz 的中断是协作式取消：只会把 context.CancellationToken 置为已取消，不会强杀线程。
    /// 作业要能被中断，必须做到三点：
    ///   ① 从 context.CancellationToken 拿令牌（不要自己 new）；
    ///   ② 所有等待 / IO 调用都把令牌传进去；
    ///   ③ 长循环里每轮用 token.ThrowIfCancellationRequested() 设检查点。
    ///
    /// 运行方式：每分钟触发一次，模拟约 60 秒的分批处理。
    /// 在「日志管理」页找到运行中的记录，点「中断」即可在下一个检查点提前结束，
    /// 日志保留「中断」状态，且不会进入启动期自动补偿（补偿状态为「已跳过」）。
    /// </summary>
    [QuartzJob("ManualInterruptJob", "DEFAULT", "0 * * * * ?", Description = "手动中断演示作业，每分钟执行一次，模拟60秒分批处理，可在日志页点「中断」提前结束")]
    public class ManualInterruptJob : IJob
    {
        private readonly ILogger<ManualInterruptJob> _logger;

        public ManualInterruptJob(ILogger<ManualInterruptJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            // ① 拿到 Quartz 注入的取消令牌
            var token = context.CancellationToken;

            _logger.LogInformation("手动中断作业开始执行: {Time}, JobKey: {JobKey}", DateTime.Now, context.JobDetail.Key);

            const int totalBatches = 10;
            var completedBatches = 0;

            try
            {
                for (int i = 1; i <= totalBatches; i++)
                {
                    // ③ 长循环里自己设检查点：每轮开始前检查一次，
                    //    点击中断后最多再处理一个批次就会退出
                    token.ThrowIfCancellationRequested();

                    _logger.LogInformation("手动中断作业进度: 第 {Current}/{Total} 批", i, totalBatches);

                    // ② 所有等待 / IO 调用都把令牌传进去
                    //    对比错误写法：await Task.Delay(6000); —— 不传令牌则无法被中断
                    await Task.Delay(TimeSpan.FromSeconds(6), token);

                    // 模拟单批 IO 操作（实际项目中替换为数据库写入、HTTP 调用等，同样传入令牌）
                    await ProcessBatchAsync(i, token);
                    completedBatches++;
                }

                _logger.LogInformation("手动中断作业执行完成: {Time}, 共处理 {Count} 批", DateTime.Now, completedBatches);
            }
            // 不要把取消当成业务失败：用户手动中断 / 调度器停机属于正常退出，
            // 必须原样抛出 OperationCanceledException，平台才能保留「中断」状态
            // （吞掉该异常会被监听器记成成功，导致中断状态被覆盖）
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // 不要在 catch 里做长时间收尾，快速记录后立刻退出
                _logger.LogWarning("手动中断作业被取消: {Time}, 已完成 {Count}/{Total} 批",
                    DateTime.Now, completedBatches, totalBatches);
                throw;
            }
            catch (Exception ex)
            {
                // 真正的业务异常：记录后原样抛出，让 Quartz 记录失败状态
                _logger.LogError(ex, "手动中断作业执行失败: {Time}, 已完成 {Count}/{Total} 批",
                    DateTime.Now, completedBatches, totalBatches);
                throw;
            }
        }

        /// <summary>
        /// 模拟单批业务处理
        /// </summary>
        private async Task ProcessBatchAsync(int batch, CancellationToken token)
        {
            await Task.Delay(500, token);

            _logger.LogInformation("手动中断作业第 {Batch} 批处理完成", batch);
        }
    }
}
