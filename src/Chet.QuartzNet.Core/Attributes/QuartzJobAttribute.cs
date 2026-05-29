namespace Chet.QuartzNet.Core.Attributes
{
    /// <summary>
    /// Quartz作业特性，用于标记作业类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class QuartzJobAttribute : Attribute
    {
        /// <summary>
        /// 作业名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 作业分组
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Cron表达式
        /// </summary>
        public string CronExpression { get; set; }

        /// <summary>
        /// 作业描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">作业名称</param>
        /// <param name="group">作业分组</param>
        /// <param name="cronExpression">Cron表达式</param>
        /// <param name="description">作业描述</param>
        public QuartzJobAttribute(
            string name,
            string group,
            string cronExpression,
            string description = "",
            bool enabled = true
        )
        {
            Name = name;
            Group = group;
            CronExpression = cronExpression;
            Description = description;
            Enabled = enabled;
        }
    }
}
