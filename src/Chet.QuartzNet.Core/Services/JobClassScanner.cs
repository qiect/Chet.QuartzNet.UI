using System.Reflection;
using Quartz;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// 作业类扫描服务，用于扫描实现了IJob接口的类
/// </summary>
public class JobClassScanner
{
    private List<string> _jobClassNames = new List<string>();
    private readonly object _lock = new object();

    /// <summary>
    /// 扫描所有实现了IJob接口的类
    /// </summary>
    public List<string> ScanJobClasses()
    {
        lock (_lock)
        {
            if (_jobClassNames.Count > 0)
            {
                return _jobClassNames.OrderBy(x => x).ToList();
            }

            try
            {
                // 获取当前应用程序域中所有已加载的程序集
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var jobClasses = new List<string>();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // 跳过系统程序集和第三方程序集
                        if (assembly.IsDynamic || 
                            assembly.FullName?.StartsWith("System.") == true ||
                            assembly.FullName?.StartsWith("Microsoft.") == true ||
                            assembly.FullName?.StartsWith("Quartz") == true ||
                            assembly.FullName?.StartsWith("Newtonsoft.") == true ||
                            assembly.FullName?.StartsWith("AntDesign") == true ||
                            assembly.FullName?.StartsWith("Vue") == true)
                        {
                            continue;
                        }

                        // 查找实现了IJob接口的非抽象类，只排除核心库中的类
                        var types = assembly.GetTypes()
                            .Where(t => typeof(IJob).IsAssignableFrom(t) && 
                                       !t.IsAbstract && 
                                       !t.IsInterface && 
                                       t.IsPublic &&
                                       !(t.Namespace?.StartsWith("Chet.QuartzNet.Core") == true || t.Namespace?.StartsWith("Chet.QuartzNet.UI") == true))
                            .Select(t => t.FullName)
                            .Where(name => !string.IsNullOrEmpty(name))
                            .ToList();

                        jobClasses.AddRange(types);
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // 忽略无法加载的程序集
                        continue;
                    }
                    catch (Exception)
                    {
                        // 忽略其他异常
                        continue;
                    }
                }

                // 去重并排序
                _jobClassNames = jobClasses.Distinct().OrderBy(x => x).ToList();
                return _jobClassNames;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }
}
