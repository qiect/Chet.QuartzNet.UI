// Quartz.Net 管理UI前端应用 - Vue 2 + Ant Design Vue 1.x版本

// 注册Ant Design Vue组件
Vue.use(antd);

// 创建Vue应用
new Vue({
    el: '#app',
    data() {
        return {
            // 状态管理
            loading: false,
            schedulerStatus: { isRunning: false, started: false },
            jobs: [],
            logs: [],
            activeTab: 'jobs',

            // 主题设置
            isDarkTheme: false,

            // 分页配置
            jobPagination: {
                current: 1,
                pageSize: 10,
                total: 0,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: total => `共 ${total} 条记录`
            },

            logPagination: {
                current: 1,
                pageSize: 10,
                total: 0,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: total => `共 ${total} 条记录`
            },

            // 排序状态
            jobSorter: null,
            logSorter: null,

            // 日志搜索表单
            logSearchForm: {
                jobName: '',
                jobGroup: '',
                status: '',
                startTime: null,
                endTime: null
            },


            // 搜索表单
            searchForm: {
                jobName: '',
                jobGroup: '',
                status: null
            },

            // 模态框状态
            modalVisible: false,
            modalTitle: '添加作业',
            currentJob: {
                jobName: '',
                jobGroup: 'DEFAULT',
                triggerName: '',
                triggerGroup: 'DEFAULT',
                cronExpression: '0 0/1 * * * ?',
                description: '',
                jobType: '',
                jobTypeEnum: 'DLL', // 默认DLL作业
                jobData: '',
                isEnabled: true,
                // API作业字段
                apiMethod: 'GET',
                apiHeaders: '',
                apiBody: '',
                apiTimeout: 30,
                skipSslValidation: false
            },

            // 定时器
            refreshTimer: null,

            // Cron表达式帮助模态框状态
            cronHelperVisible: false,

            // 作业类名列表
            jobClasses: [],

            // 是否正在加载作业类名
            loadingJobClasses: false,

            // 常用Cron表达式列表
            commonCronExpressions: [
                { name: '每分钟', expression: '0 * * * * ?', description: '每分钟执行一次' },
                { name: '每30分钟', expression: '0 0/30 * * * ?', description: '每30分钟执行一次' },
                { name: '每小时', expression: '0 0 * * * ?', description: '每小时执行一次' },
                { name: '每2小时', expression: '0 0 0/2 * * ?', description: '每2小时执行一次' },
                { name: '每天凌晨1点', expression: '0 0 1 * * ?', description: '每天凌晨1点执行' },
                { name: '每周一上午8点', expression: '0 0 8 ? * MON', description: '每周一上午8点执行' },
                { name: '每周五下午5点', expression: '0 0 17 ? * FRI', description: '每周五下午5点执行' },
                { name: '每月1号上午8点', expression: '0 0 8 1 * ?', description: '每月1号上午8点执行' }
            ],

            // 列拖动组件配置
            components: {
                header: {
                    cell: (h, props, children) => {
                        const { key, ...restProps } = props;
                        return h('th', {
                            ...restProps,
                            class: 'ant-table-cell',
                            style: {
                                resize: 'horizontal',
                                overflow: 'hidden',
                                minWidth: '80px',
                                maxWidth: '500px',
                                position: 'relative'
                            }
                        }, children);
                    }
                }
            }
        };
    },

    computed: {
        // 作业表格列配置
        jobColumns() {
            return [
                {
                    title: '作业名称',
                    dataIndex: 'jobName',
                    key: 'jobName',
                    width: 150,
                    sorter: true,
                    fixed: 'left'
                },
                {
                    title: '作业分组',
                    dataIndex: 'jobGroup',
                    key: 'jobGroup',
                    width: 120,
                    sorter: true
                },
                {
                    title: 'Cron表达式',
                    dataIndex: 'cronExpression',
                    key: 'cronExpression',
                    width: 150
                },
                {
                    title: '作业类型',
                    dataIndex: 'jobType',
                    key: 'jobType',
                    ellipsis: true
                },
                {
                    title: '状态',
                    dataIndex: 'status',
                    key: 'status',
                    width: 80,
                    align: 'center',
                    scopedSlots: { customRender: 'status' }
                },
                {
                    title: '启用',
                    dataIndex: 'isEnabled',
                    key: 'isEnabled',
                    width: 80,
                    align: 'center',
                    scopedSlots: { customRender: 'isEnabled' }
                },
                {
                    title: '下次执行',
                    dataIndex: 'nextRunTime',
                    key: 'nextRunTime',
                    width: 150,
                    scopedSlots: { customRender: 'nextRunTime' },
                    sorter: true
                },
                {
                    title: '创建时间',
                    dataIndex: 'createTime',
                    key: 'createTime',
                    width: 150,
                    scopedSlots: { customRender: 'createTime' },
                    sorter: true
                },
                {
                    title: '操作',
                    key: 'action',
                    width: 250,
                    fixed: 'right',
                    scopedSlots: { customRender: 'action' }
                }
            ];
        },

        // 日志表格列配置
        logColumns() {
            return [
                {
                    title: '日志ID',
                    dataIndex: 'logId',
                    key: 'logId',
                    width: 200,
                    ellipsis: true,
                    tooltip: true,
                    sorter: true
                },
                {
                    title: '作业名称',
                    dataIndex: 'jobName',
                    key: 'jobName',
                    width: 150,
                    sorter: true
                },
                {
                    title: '作业分组',
                    dataIndex: 'jobGroup',
                    key: 'jobGroup',
                    width: 120,
                    sorter: true
                },
                {
                    title: '状态',
                    dataIndex: 'status',
                    key: 'status',
                    width: 80,
                    align: 'center',
                    scopedSlots: { customRender: 'status' }
                },
                {
                    title: '开始时间',
                    dataIndex: 'startTime',
                    key: 'startTime',
                    width: 150,
                    scopedSlots: { customRender: 'startTime' },
                    sorter: true
                },
                {
                    title: '结束时间',
                    dataIndex: 'endTime',
                    key: 'endTime',
                    width: 150,
                    scopedSlots: { customRender: 'endTime' }
                },
                {
                    title: '耗时',
                    dataIndex: 'duration',
                    key: 'duration',
                    width: 100,
                    align: 'center',
                    scopedSlots: { customRender: 'duration' }
                },
                {
                    title: '错误信息',
                    dataIndex: 'errorMessage',
                    key: 'errorMessage',
                    ellipsis: true,
                    scopedSlots: { customRender: 'errorMessage' }
                }
            ];
        }
    },

    methods: {
        // 获取调度器状态
        async fetchSchedulerStatus() {
            try {
                console.log('开始获取调度器状态...');
                const response = await axios.get('/api/quartz/GetSchedulerStatus').catch(error => {
                    console.error('获取调度器状态失败:', error);
                    if (error.response) {
                        console.error('错误响应:', error.response);
                        console.error('错误状态码:', error.response.status);
                        console.error('错误URL:', error.response.config?.url);
                    }
                    throw error;
                });
                console.log('调度器状态响应:', response);
                if (response.data.success) {
                    // 映射后端状态到前端期望的格式
                    const backendData = response.data.data;
                    this.schedulerStatus = {
                        isRunning: !backendData.isShutdown && backendData.isStarted || false,
                        started: !backendData.isShutdown && backendData.isStarted || false,
                        // 保留其他字段用于显示
                        ...backendData
                    };
                    console.log('映射后的调度器状态:', this.schedulerStatus);
                } else {
                    console.error('API返回错误:', response.data.message);
                }
            } catch (error) {
                console.error('获取调度器状态失败:', error);
                this.$message.error('获取调度器状态失败: ' + (error.message || '网络错误'));

                // 当调度器停止或出现错误时，更新状态为已停止
                this.schedulerStatus = {
                    isRunning: false,
                    started: false,
                    status: '已停止'
                };
            }
        },

        // 获取作业列表
        async fetchJobs(page = 1, pageSize = 10, searchParams = null, sorter = null) {
            this.loading = true;
            try {
                console.log('开始获取作业列表...', { page, pageSize, searchForm: this.searchForm });
                const params = {
                    jobName: searchParams?.jobName || this.searchForm.jobName,
                    jobGroup: searchParams?.jobGroup || this.searchForm.jobGroup,
                    status: searchParams?.status || this.searchForm.status,
                    pageIndex: page,
                    pageSize: pageSize,
                    sortBy: sorter?.field || '',
                    sortOrder: sorter?.order === 'ascend' ? 'asc' : sorter?.order === 'descend' ? 'desc' : ''
                };
                console.log('请求参数:', params);
                const response = await axios.post('/api/quartz/GetJobs', params);
                console.log('作业列表响应:', response);
                if (response.data.success && response.data.data) {
                    this.jobs = response.data.data.items || [];
                    this.jobPagination.total = response.data.data.totalCount || 0;
                    this.jobPagination.current = page;
                    this.jobPagination.pageSize = pageSize;
                    console.log('作业列表数据:', this.jobs);
                } else {
                    console.error('获取作业列表API返回错误:', response.data.message);
                    this.$message.error('获取作业列表失败: ' + response.data.message);
                }
            } catch (error) {
                console.error('获取作业列表失败:', error);
                this.$message.error('获取作业列表失败: ' + (error.message || '网络错误'));
            } finally {
                this.loading = false;
            }
        },

        // 获取作业日志
        async fetchLogs(page = 1, pageSize = 10, searchParams = null, sorter = null) {
            this.loading = true;
            try {
                // 使用提供的搜索参数或当前表单数据
                const params = {
                    pageIndex: page,
                    pageSize: pageSize,
                    jobName: searchParams?.jobName || this.logSearchForm.jobName,
                    jobGroup: searchParams?.jobGroup || this.logSearchForm.jobGroup,
                    status: searchParams?.status !== undefined ? searchParams.status : this.logSearchForm.status,
                    startTime: searchParams?.startTime || this.logSearchForm.startTime,
                    endTime: searchParams?.endTime || this.logSearchForm.endTime,
                    sortBy: sorter?.field || '',
                    sortOrder: sorter?.order === 'ascend' ? 'asc' : sorter?.order === 'descend' ? 'desc' : ''
                };

                // 处理日期对象，转换为ISO字符串
                if (params.startTime instanceof Date) {
                    params.startTime = params.startTime.toISOString();
                }
                if (params.endTime instanceof Date) {
                    params.endTime = params.endTime.toISOString();
                }
                
                // 确保status参数类型正确（转换为数字或null）
                if (params.status === '' || params.status === null || params.status === undefined) {
                    params.status = null;
                } else {
                    params.status = parseInt(params.status);
                }

                const response = await axios.post('/api/quartz/GetJobLogs', params);
                if (response.data.success && response.data.data) {
                    this.logs = response.data.data.items || [];
                    this.logPagination.total = response.data.data.totalCount || 0;
                    this.logPagination.current = page;
                    this.logPagination.pageSize = pageSize;
                }
            } catch (error) {
                this.$message.error('获取作业日志失败');
            } finally {
                this.loading = false;
            }
        },

        // 获取作业类名列表
        async fetchJobClasses() {
            try {
                this.loadingJobClasses = true;
                const response = await axios.get('/api/quartz/GetJobClasses');
                if (response.data.success) {
                    this.jobClasses = response.data.data || [];
                } else {
                    this.$message.error('获取作业类名列表失败: ' + response.data.message);
                }
            } catch (error) {
                console.error('获取作业类名列表失败:', error);
                this.$message.error('获取作业类名列表失败: ' + (error.message || '网络错误'));
            } finally {
                this.loadingJobClasses = false;
            }
        },

        // 切换调度器状态
        async toggleScheduler() {
            try {
                if (this.schedulerStatus.isRunning) {
                    const response = await axios.post('/api/quartz/StopScheduler');
                    if (response.data.success) {
                        this.$message.success('调度器已停止');
                        this.fetchSchedulerStatus();
                    } else {
                        this.$message.error(response.data.message);
                    }
                } else {
                    const response = await axios.post('/api/quartz/StartScheduler');
                    if (response.data.success) {
                        this.$message.success('调度器已启动');
                        this.fetchSchedulerStatus();
                    } else {
                        this.$message.error(response.data.message);
                    }
                }
            } catch (error) {
                this.$message.error('操作失败: ' + (error.message || '网络错误'));
            }
        },

        // 刷新数据
        refreshData() {
            this.fetchSchedulerStatus();
            if (this.activeTab === 'jobs') {
                this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize);
            } else if (this.activeTab === 'logs') {
                this.fetchLogs(this.logPagination.current, this.logPagination.pageSize);
            }
        },

        // 显示添加作业模态框（触发器信息将由后端自动生成）
        showAddModal() {
            this.currentJob = {
                jobName: '',
                jobGroup: 'DEFAULT',
                cronExpression: '0 0/1 * * * ?',
                description: '',
                jobType: '',
                jobTypeEnum: 'DLL',
                jobData: '',
                isEnabled: true,
                // API作业字段
                apiMethod: 'GET',
                apiHeaders: '',
                apiBody: '',
                apiTimeout: 30,
                skipSslValidation: false
            };
            this.isEditing = false;
            this.modalTitle = '添加作业';
            this.modalVisible = true;

            // 加载作业类名列表
            this.fetchJobClasses();
        },

        // 显示编辑作业模态框
        showEditModal(job) {
            this.currentJob = { ...job };
            this.isEditing = true;
            this.modalTitle = '编辑作业';
            this.modalVisible = true;
            // 加载作业类名列表
            this.fetchJobClasses();
        },

        // 保存作业
        async handleSave() {
            try {
                // 验证必填字段（触发器信息将自动生成）
                if (!this.currentJob.jobName || !this.currentJob.jobGroup || !this.currentJob.cronExpression || !this.currentJob.jobType) {
                    this.$message.error('请填写必填字段');
                    return;
                }

                // 转换字段名以匹配后端DTO（触发器信息将由后端自动生成）
                const jobData = {
                    JobName: this.currentJob.jobName,
                    JobGroup: this.currentJob.jobGroup,
                    CronExpression: this.currentJob.cronExpression,
                    Description: this.currentJob.description,
                    JobType: this.currentJob.jobType,
                    JobTypeEnum: this.currentJob.jobTypeEnum,
                    JobData: this.currentJob.jobData,
                    IsEnabled: this.currentJob.isEnabled,
                    // API作业字段
                    ApiMethod: this.currentJob.apiMethod,
                    ApiHeaders: this.currentJob.apiHeaders,
                    ApiBody: this.currentJob.apiBody,
                    ApiTimeout: this.currentJob.apiTimeout,
                    SkipSslValidation: this.currentJob.skipSslValidation
                };

                const response = this.isEditing
                    ? await axios.put('/api/quartz/UpdateJob', jobData)
                    : await axios.post('/api/quartz/AddJob', jobData);

                if (response.data.success) {
                    this.$message.success(response.data.message);
                    this.modalVisible = false;
                    this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize);
                } else {
                    this.$message.error(response.data.message);
                }
            } catch (error) {
                this.$message.error('保存作业失败: ' + (error.message || '网络错误'));
            }
        },

        // 模态框取消
        handleModalCancel() {
            this.modalVisible = false;
        },

        // 删除作业
        async handleDelete(job) {
            try {
                const response = await axios.delete('/api/quartz/DeleteJob', {
                    params: { jobName: job.jobName, jobGroup: job.jobGroup }
                });
                if (response.data.success) {
                    this.$message.success('删除成功');
                    this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize);
                } else {
                    this.$message.error(response.data.message);
                }
            } catch (error) {
                this.$message.error('删除失败: ' + (error.message || '网络错误'));
            }
        },

        // 暂停作业
        async handlePause(job) {
            try {
                const response = await axios.post('/api/quartz/PauseJob', null, {
                    params: { jobName: job.jobName, jobGroup: job.jobGroup }
                });
                if (response.data.success) {
                    this.$message.success('暂停成功');
                    this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize);
                } else {
                    this.$message.error(response.data.message);
                }
            } catch (error) {
                this.$message.error('暂停失败: ' + (error.message || '网络错误'));
            }
        },

        // 恢复作业
        async handleResume(job) {
            try {
                const response = await axios.post('/api/quartz/ResumeJob', null, {
                    params: { jobName: job.jobName, jobGroup: job.jobGroup }
                });
                if (response.data.success) {
                    this.$message.success('恢复成功');
                    this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize);
                } else {
                    this.$message.error(response.data.message);
                }
            } catch (error) {
                this.$message.error('恢复失败: ' + (error.message || '网络错误'));
            }
        },

        // 触发作业
        async handleTrigger(job) {
            try {
                const response = await axios.post('/api/quartz/TriggerJob', null, {
                    params: { jobName: job.jobName, jobGroup: job.jobGroup }
                });
                if (response.data.success) {
                    this.$message.success('触发成功');
                    this.fetchLogs(1, this.logPagination.pageSize);
                    this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize); // 重新获取作业列表，更新下次执行时间
                } else {
                    this.$message.error(response.data.message);
                }
            } catch (error) {
                this.$message.error('触发失败: ' + (error.message || '网络错误'));
            }
        },

        // 搜索
        handleSearch() {
            this.fetchJobs(1, this.jobPagination.pageSize);
        },

        // 重置搜索
        resetSearch() {
            this.searchForm = {
                jobName: '',
                jobGroup: '',
                status: null
            };
            this.fetchJobs(1, this.jobPagination.pageSize);
        },

        // 标签页切换
        handleTabChange(activeKey) {
            this.activeTab = activeKey;
            if (activeKey === 'jobs') {
                this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize, null, this.jobSorter);
            } else if (activeKey === 'logs') {
                this.fetchLogs(this.logPagination.current, this.logPagination.pageSize, null, this.logSorter);
            }
        },

        // 作业表格变化
        handleJobTableChange(pagination, filters, sorter) {
            this.jobPagination.current = pagination.current;
            this.jobPagination.pageSize = pagination.pageSize;
            this.jobSorter = sorter; // 保存排序状态
            this.fetchJobs(pagination.current, pagination.pageSize, null, sorter);
        },

        // 日志表格变化
        handleLogTableChange(pagination, filters, sorter) {
            this.logPagination.current = pagination.current;
            this.logPagination.pageSize = pagination.pageSize;
            this.logSorter = sorter; // 保存排序状态
            this.fetchLogs(pagination.current, pagination.pageSize, null, sorter);
        },

        // 处理日志搜索
        handleLogSearch() {
            // 重置到第一页
            this.fetchLogs(1, this.logPagination.pageSize);
        },

        // 重置日志搜索条件
        handleLogReset() {
            this.logSearchForm = {
                jobName: '',
                jobGroup: '',
                status: '',
                startTime: null,
                endTime: null
            };
            // 重置后重新获取第一页数据
            this.fetchLogs(1, this.logPagination.pageSize);
        },

        // 状态颜色映射
        getJobStatusColor(status) {
            const statusMap = {
                0: 'green',    // Normal
                1: 'orange',   // Paused
                2: 'blue',     // Complete
                3: 'red',      // Error
                4: 'cyan'      // Blocked
            };
            return statusMap[status] || 'blue';
        },

        getJobStatusText(status) {
            const statusMap = {
                0: '正常',
                1: '暂停',
                2: '完成',
                3: '错误',
                4: '阻塞'
            };
            return statusMap[status] || '未知';
        },

        getLogStatusColor(status) {
            const statusMap = {
                0: 'blue',     // Running
                1: 'green',    // Success
                2: 'red'       // Failed
            };
            return statusMap[status] || 'blue';
        },

        getLogStatusText(status) {
            const statusMap = {
                0: '运行中',
                1: '成功',
                2: '失败'
            };
            return statusMap[status] || '未知';
        },

        // 格式化日期时间
        formatDateTime(dateTimeStr) {
            if (!dateTimeStr) return '-';
            try {
                const date = new Date(dateTimeStr);
                if (isNaN(date.getTime())) return dateTimeStr;
                return date.toLocaleString('zh-CN', {
                    year: 'numeric',
                    month: '2-digit',
                    day: '2-digit',
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit'
                });
            } catch (error) {
                return dateTimeStr || '-';
            }
        },

        // 切换主题
        toggleTheme() {
            this.isDarkTheme = !this.isDarkTheme;
            this.applyTheme(this.isDarkTheme);
            // 保存主题设置到localStorage
            localStorage.setItem('theme', this.isDarkTheme ? 'dark' : 'light');
        },

        // 应用主题
        applyTheme(dark) {
            if (dark) {
                document.documentElement.setAttribute('data-theme', 'dark');
            } else {
                document.documentElement.removeAttribute('data-theme');
            }
        },

        // 检测系统主题
        detectSystemTheme() {
            return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        },

        // 显示Cron表达式帮助模态框
        showCronHelper() {
            this.cronHelperVisible = true;
        },

        // 关闭Cron表达式帮助模态框
        handleCronHelperOk() {
            this.cronHelperVisible = false;
        },

        // 取消Cron表达式帮助模态框
        handleCronHelperCancel() {
            this.cronHelperVisible = false;
        },

        // 选择Cron表达式
        selectCronExpression(expression) {
            this.currentJob.cronExpression = expression;
            this.cronHelperVisible = false;
            this.$message.success('Cron表达式已更新');
        },

        // 验证Cron表达式格式
        validateCronExpression(expression) {
            if (!expression || typeof expression !== 'string') {
                return false;
            }

            // 基本格式验证：6或7个字段，用空格分隔
            const parts = expression.trim().split(/\s+/);
            if (parts.length !== 6 && parts.length !== 7) {
                return false;
            }

            // 验证每个字段
            const patterns = [
                /^[0-9,\-*\/]+$/,
                /^[0-9,\-*\/]+$/,
                /^[0-9,\-*\/]+$/,
                /^[0-9,\-*\/]+$/,
                /^[0-9,\-*\/]+$/,
                /^[0-9,\-*\/]+$/,
                /^[0-9,\-*\/]+$/
            ];

            return parts.every((part, index) => {
                if (index >= patterns.length) return true;
                return patterns[index].test(part);
            });
        }
    },

    mounted() {
        console.log('Vue 2应用初始化...');

        // 初始化主题
        const savedTheme = localStorage.getItem('theme');
        if (savedTheme) {
            this.isDarkTheme = savedTheme === 'dark';
        } else {
            this.isDarkTheme = this.detectSystemTheme();
        }
        this.applyTheme(this.isDarkTheme);

        // 监听系统主题变化
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
                if (!localStorage.getItem('theme')) {
                    this.isDarkTheme = e.matches;
                    this.applyTheme(this.isDarkTheme);
                }
            });
        }
        this.fetchSchedulerStatus();
        this.fetchJobs();

        // 定时刷新数据
        this.refreshTimer = setInterval(() => {
            this.fetchSchedulerStatus();
            if (this.activeTab === 'jobs') {
                this.fetchJobs(this.jobPagination.current, this.jobPagination.pageSize, null, this.jobSorter);
            } else if (this.activeTab === 'logs') {
                this.fetchLogs(this.logPagination.current, this.logPagination.pageSize, null, this.logSorter);
            }
        }, 5000);
    },

    beforeDestroy() {
        if (this.refreshTimer) {
            clearInterval(this.refreshTimer);
        }
    }
});

// 配置axios基础URL - 使用相对路径，从根目录开始
axios.defaults.baseURL = '/';

// 添加axios请求和响应拦截器
axios.interceptors.request.use(
    config => {
        console.log('Axios请求:', config.method, config.url, config.data);
        return config;
    },
    error => {
        console.error('Axios请求错误:', error);
        return Promise.reject(error);
    }
);

axios.interceptors.response.use(
    response => {
        console.log('Axios响应:', response.status, response.data);
        return response;
    },
    error => {
        console.error('Axios响应错误:', error);
        if (error.response) {
            console.error('错误状态码:', error.response.status);
            console.error('错误数据:', error.response.data);
        }
        return Promise.reject(error);
    }
);