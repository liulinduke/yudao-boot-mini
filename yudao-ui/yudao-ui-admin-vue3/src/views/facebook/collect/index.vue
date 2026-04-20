<template>
  <div>
    <el-row :gutter="20">
      <!-- 左侧：功能卡片 -->
      <el-col :span="8">
        <ContentWrap>
          <div class="function-section">
            <h3 class="section-title">采集功能</h3>
            <div class="function-grid">
              <FunctionCard
                v-for="(func, index) in functions"
                :key="index"
                :title="func.title"
                :icon="func.icon"
                :description="func.description"
                :active="activeFunction === func.type"
                :disabled="func.disabled"
                @click="selectFunction(func.type)"
              />
            </div>
          </div>
        </ContentWrap>
      </el-col>

      <!-- 右侧：任务列表 -->
      <el-col :span="16">
        <ContentWrap>
          <!-- 搜索工作栏 -->
          <el-form
            class="-mb-15px"
            :model="queryParams"
            ref="queryFormRef"
            :inline="true"
            label-width="68px"
          >
            <el-form-item label="任务类型" prop="taskType">
              <el-select
                v-model="queryParams.taskType"
                placeholder="请选择任务类型"
                clearable
                class="!w-140px"
              >
                <el-option
                  v-for="dict in getIntDictOptions(DICT_TYPE.FB_SEARCH_TYPE)"
                  :key="dict.value"
                  :label="dict.label"
                  :value="dict.value"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="状态" prop="status">
              <el-select
                v-model="queryParams.status"
                placeholder="请选择状态"
                clearable
                class="!w-140px"
              >
                <el-option
                  v-for="dict in getIntDictOptions(DICT_TYPE.SYS_COLLECT_STATUS)"
                  :key="dict.value"
                  :label="dict.label"
                  :value="dict.value"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="备注" prop="remark">
              <el-input
                v-model="queryParams.remark"
                placeholder="请输入备注"
                clearable
                @keyup.enter="handleQuery"
                class="!w-140px"
              />
            </el-form-item>
            <el-form-item label="创建时间" prop="createTime">
              <el-date-picker
                v-model="queryParams.createTime"
                value-format="YYYY-MM-DD HH:mm:ss"
                type="daterange"
                start-placeholder="开始日期"
                end-placeholder="结束日期"
                :default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
                class="!w-220px"
              />
            </el-form-item>
            <el-form-item>
              <el-button @click="handleQuery"
                ><Icon icon="ep:search" class="mr-5px" /> 搜索</el-button
              >

              <el-button
                type="danger"
                plain
                :disabled="isEmpty(checkedIds)"
                @click="handleDeleteBatch"
                v-hasPermi="['facebook:fb-collect:delete']"
              >
                <Icon icon="ep:delete" class="mr-5px" /> 批量删除
              </el-button>
            </el-form-item>
          </el-form>

          <!-- 列表 -->
          <el-table
            row-key="id"
            v-loading="loading"
            :data="list"
            :stripe="true"
            :show-overflow-tooltip="true"
            @selection-change="handleRowCheckboxChange"
          >
            <el-table-column type="selection" width="55" />
            <el-table-column label="任务类型" align="center" prop="taskType" width="100">
              <template #default="scope">
                <dict-tag :type="DICT_TYPE.FB_SEARCH_TYPE" :value="scope.row.taskType" />
              </template>
            </el-table-column>
            <el-table-column
              label="采集链接"
              align="center"
              prop="searchUrl"
              min-width="200"
              show-overflow-tooltip
            />
            <el-table-column label="期望/已采" align="center" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}/{{ scope.row.collectedCount }}
              </template>
            </el-table-column>
            <el-table-column label="进度" align="center" width="120">
              <template #default="scope">
                <el-progress
                  :percentage="getProgress(scope.row)"
                  :status="scope.row.status === 2 ? 'success' : undefined"
                  :stroke-width="15"
                />
              </template>
            </el-table-column>
            <el-table-column label="状态" align="center" prop="status" width="100">
              <template #default="scope">
                <dict-tag :type="DICT_TYPE.SYS_COLLECT_STATUS" :value="scope.row.status" />
              </template>
            </el-table-column>
            <el-table-column
              label="创建时间"
              align="center"
              prop="createTime"
              :formatter="dateFormatter"
              width="160"
            />
            <el-table-column label="操作" align="center" width="150" fixed="right">
              <template #default="scope">
                <el-button
                  link
                  type="primary"
                  @click="openForm('update', scope.row.id)"
                  v-hasPermi="['facebook:fb-collect:update']"
                >
                  编辑
                </el-button>
                <el-button
                  link
                  type="danger"
                  @click="handleDelete(scope.row.id)"
                  v-hasPermi="['facebook:fb-collect:delete']"
                >
                  删除
                </el-button>
              </template>
            </el-table-column>
          </el-table>
          <!-- 分页 -->
          <Pagination
            :total="total"
            v-model:page="queryParams.pageNo"
            v-model:limit="queryParams.pageSize"
            @pagination="getList"
          />
        </ContentWrap>
      </el-col>
    </el-row>

    <!-- 表单弹窗：添加/修改 -->
    <FbCollectForm ref="formRef" @success="getList" />
  </div>
</template>

<script setup lang="ts">
import { getIntDictOptions, DICT_TYPE } from '@/utils/dict'
import { isEmpty } from '@/utils/is'
import { dateFormatter } from '@/utils/formatTime'
import download from '@/utils/download'
import { FbCollectApi, FbCollect } from '@/api/facebook/collect'
import { FbCollectUserApi } from '@/api/facebook/collectuser'
import FbCollectForm from './FbCollectForm.vue'
import FunctionCard from './components/FunctionCard.vue'
import { onCollectionComplete } from '@/utils/wpfBridge'

/** FB采集任务 - 左右布局 */
defineOptions({ name: 'FbCollect' })

/**
 * 解析粉丝数字符串为整数
 * 支持格式：143, 143rb, 1.5k, 2M, 10jt, 5ribu 等
 * @param followersRaw 原文（如：143rb, 1.5k, 2M）
 * @return 解析后的数字，失败返回 null
 */
const parseFollowers = (followersRaw: string): number | null => {
  if (!followersRaw) return null
  
  try {
    // 提取数字部分（包括小数点）
    const numberPart = followersRaw.replace(/[^\d.]/g, '')
    if (!numberPart) return null
    
    let number = parseFloat(numberPart)
    
    // 判断单位（不区分大小写）
    const lowerCase = followersRaw.toLowerCase()
    
    if (lowerCase.includes('rb') || lowerCase.includes('ribu') || 
        lowerCase.includes('rbu') || lowerCase.includes('천')) {
      // 千
      number *= 1000
    } else if (lowerCase.includes('jt') || lowerCase.includes('juta') || 
               lowerCase.includes('만') || lowerCase.includes('万')) {
      // 万
      number *= 10000
    } else if (lowerCase.includes('백만') || lowerCase.includes('百万')) {
      // 百万
      number *= 1000000
    } else if (lowerCase.includes('억') || lowerCase.includes('千万') || lowerCase.includes('亿')) {
      // 千万/亿
      number *= 10000000
    } else if (lowerCase.includes('조') || lowerCase.includes('万亿')) {
      // 万亿
      number *= 1000000000000
    } else if (lowerCase.includes('k')) {
      // K = 千
      number *= 1000
    } else if (lowerCase.includes('m')) {
      // M = 百万
      number *= 1000000
    } else if (lowerCase.includes('b')) {
      // B = 十亿
      number *= 1000000000
    } else if (lowerCase.includes('t')) {
      // T = 万亿
      number *= 1000000000000
    }
    
    return Math.floor(number)
  } catch (e) {
    return null
  }
}

const message = useMessage()
const { t } = useI18n()

// 功能列表
const functions = [
  {
    type: 'pages',
    title: '主页采集',
    icon: 'ep:user',
    description: '采集Facebook主页信息',
    disabled: false
  },
  {
    type: 'posts',
    title: '帖子采集',
    icon: 'ep:document',
    description: '采集Facebook帖子内容',
    disabled: false
  },
  {
    type: 'users',
    title: '用户采集',
    icon: 'ep:user-filled',
    description: '采集Facebook用户资料',
    disabled: false
  },
  {
    type: 'groups',
    title: '群组采集',
    icon: 'ep:user',
    description: '采集Facebook群组信息',
    disabled: true
  },
  {
    type: 'events',
    title: '活动采集',
    icon: 'ep:calendar',
    description: '采集Facebook活动信息',
    disabled: true
  },
  {
    type: 'comments',
    title: '评论采集',
    icon: 'ep:chat-dot-round',
    description: '采集帖子评论内容',
    disabled: true
  }
]

const activeFunction = ref('')
const loading = ref(true)
const list = ref<FbCollect[]>([])
const total = ref(0)
const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  taskType: undefined,
  status: undefined,
  remark: undefined,
  createTime: []
})
const queryFormRef = ref()
const exportLoading = ref(false)

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const data = await FbCollectApi.getFbCollectPage(queryParams)
    console.log('=== API返回数据 ===', data)
    list.value = data.list || []
    total.value = data.total || 0
  } catch (error) {
    console.error('获取列表失败:', error)
    list.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

/** 选择功能 - 打开新建任务弹窗 */
const selectFunction = (type: string) => {
  if (functions.find((f) => f.type === type)?.disabled) {
    message.warning('该功能开发中')
    return
  }
  activeFunction.value = type
  // 打开新建任务弹窗，并预设任务类型
  formRef.value.open('create', undefined, getTaskTypeByFunction(type))
}

/** 根据功能类型获取任务类型 */
const getTaskTypeByFunction = (funcType: string): number => {
  const typeMap: Record<string, number> = {
    pages: 1,
    posts: 2,
    users: 3,
    groups: 4,
    events: 5,
    comments: 6
  }
  return typeMap[funcType] || 1
}

/** 计算进度 */
const getProgress = (task: FbCollect) => {
  if (!task.expectedCount || task.expectedCount === 0) {
    return 0
  }
  return Math.min(100, Math.round((task.collectedCount / task.expectedCount) * 100))
}

/** 搜索按钮操作 */
const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

/** 重置按钮操作 */
const resetQuery = () => {
  queryFormRef.value.resetFields()
  handleQuery()
}

/** 处理新增命令 */
const handleCreateCommand = (funcType: string) => {
  const func = functions.find((f) => f.type === funcType)
  if (!func || func.disabled) {
    message.warning('该功能开发中')
    return
  }
  // 打开新建任务弹窗，并预设任务类型
  formRef.value.open('create', undefined, getTaskTypeByFunction(funcType))
}

/** 添加/修改操作 */
const formRef = ref()
const openForm = (type: string, id?: number) => {
  formRef.value.open(type, id)
}

/** 删除按钮操作 */
const handleDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await FbCollectApi.deleteFbCollect(id)
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

/** 批量删除FB采集任务 */
const checkedIds = ref<number[]>([])
const handleRowCheckboxChange = (records: FbCollect[]) => {
  checkedIds.value = records.map((item) => item.id!)
}

const handleDeleteBatch = async () => {
  try {
    await message.delConfirm()
    await FbCollectApi.deleteFbCollectList(checkedIds.value)
    checkedIds.value = []
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

/** 导出按钮操作 */
const handleExport = async () => {
  try {
    await message.exportConfirm()
    exportLoading.value = true
    const data = await FbCollectApi.exportFbCollect(queryParams)
    download.excel(data, 'FB采集任务.xls')
  } catch {
  } finally {
    exportLoading.value = false
  }
}

/** 初始化：监听采集完成事件并保存结果 */
onMounted(() => {
  getList()

  // 注册采集完成事件监听
  onCollectionComplete(async (data) => {
    console.log('收到采集完成事件:', data)

    try {
      const results = data.results || []
      console.log(`📊 采集数据: ${results.length} 条`)

      // 前端解析 followers：将字符串转换为数字
      const parsedResults = results.map((item: any) => {
        if (item.followers && typeof item.followers === 'string') {
          item.followers = parseFollowers(item.followers)
        }
        return item
      })

      // 调用封装好的API保存采集结果
      await FbCollectUserApi.batchSaveFbCollectUser({
        taskId: data.taskId,
        results: parsedResults
      })

      message.success(`任务 ${data.taskId} 采集完成，共采集 ${parsedResults.length} 条数据`)

      // 刷新列表
      await getList()
    } catch (error) {
      console.error('保存采集结果失败:', error)
      message.error('保存采集结果失败')
    }
  })
})
</script>

<style scoped lang="scss">
.function-section {
  .section-title {
    margin: 0 0 16px 0;
    color: var(--el-text-color-primary);
    font-size: 16px;
    font-weight: 600;
    position: relative;
    padding-left: 12px;

    &::before {
      content: '';
      position: absolute;
      left: 0;
      top: 50%;
      transform: translateY(-50%);
      width: 4px;
      height: 16px;
      background-color: var(--el-color-primary);
      border-radius: 2px;
    }
  }

  .function-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
  }
}
</style>
