<template>
  <div class="agent-page">
    <el-row :gutter="16">
      <el-col :span="10">
        <ContentWrap>
          <div class="section-header">
            <div>
              <div class="section-title">AI获客Agent</div>
              <div class="section-subtitle">租户全局配置，一套Agent统一调度采集、分析和触达</div>
            </div>
            <el-tag :type="formData.status === 1 ? 'success' : 'info'">
              {{ formData.status === 1 ? '已启用' : '已停用' }}
            </el-tag>
          </div>

          <el-form ref="formRef" :model="formData" :rules="rules" label-width="110px" class="mt-18px">
            <el-form-item label="Agent名称" prop="agentName">
              <el-input v-model="formData.agentName" placeholder="例如：外贸获客AI业务员" />
            </el-form-item>

            <el-form-item label="关键词种子">
              <el-select
                v-model="formState.seedKeywordList"
                multiple
                filterable
                allow-create
                default-first-option
                placeholder="输入3-5个核心关键词"
              >
                <el-option
                  v-for="item in formState.seedKeywordList"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="产品知识库">
              <el-select
                v-model="formState.knowledgeIdList"
                multiple
                filterable
                placeholder="选择产品说明、FAQ、官网资料等知识库"
              >
                <el-option
                  v-for="item in knowledgeList"
                  :key="item.id"
                  :label="item.name"
                  :value="item.id"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="执行账号池">
              <el-select
                v-model="formState.accountIdList"
                multiple
                filterable
                placeholder="选择用于评论/私信的FB账号"
              >
                <el-option
                  v-for="item in accountList"
                  :key="item.id"
                  :label="item.fbAccount || String(item.id)"
                  :value="item.id"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="监控群组">
              <el-select
                v-model="formState.monitorGroupList"
                multiple
                filterable
                allow-create
                default-first-option
                placeholder="输入群组ID或群组链接，回车添加"
              >
                <el-option
                  v-for="item in formState.monitorGroupList"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="线索评分">
              <el-select v-model="formData.leadScoreWorkflowId" clearable filterable placeholder="选择AI工作流">
                <el-option
                  v-for="item in workflowList"
                  :key="item.id"
                  :label="item.name"
                  :value="item.id"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="评论生成">
              <el-select v-model="formData.commentWorkflowId" clearable filterable placeholder="选择AI工作流">
                <el-option
                  v-for="item in workflowList"
                  :key="item.id"
                  :label="item.name"
                  :value="item.id"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="私信生成">
              <el-select v-model="formData.dmWorkflowId" clearable filterable placeholder="选择AI工作流">
                <el-option
                  v-for="item in workflowList"
                  :key="item.id"
                  :label="item.name"
                  :value="item.id"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="目标国家">
              <el-select
                v-model="formState.targetCountryList"
                multiple
                filterable
                allow-create
                default-first-option
                placeholder="例如 United States、Germany、Japan"
              >
                <el-option
                  v-for="item in formState.targetCountryList"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="目标语言">
              <el-select
                v-model="formState.targetLanguageList"
                multiple
                filterable
                allow-create
                default-first-option
                placeholder="例如 English、German、Japanese"
              >
                <el-option
                  v-for="item in formState.targetLanguageList"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </el-form-item>

            <el-form-item label="自动触达">
              <div class="switch-row">
                <el-switch v-model="formData.autoCommentEnabled" active-text="评论" />
                <el-switch v-model="formData.autoDmEnabled" active-text="私信/Messenger" />
              </div>
            </el-form-item>

            <el-form-item label="每日上限">
              <div class="limit-row">
                <el-input-number v-model="formData.dailyCommentLimit" :min="0" :max="1000" />
                <span>评论</span>
                <el-input-number v-model="formData.dailyDmLimit" :min="0" :max="1000" />
                <span>私信</span>
              </div>
            </el-form-item>

            <el-form-item label="随机延迟">
              <div class="limit-row">
                <el-input-number v-model="formState.replyDelayMin" :min="0" :max="86400" />
                <span>至</span>
                <el-input-number v-model="formState.replyDelayMax" :min="0" :max="86400" />
                <span>秒</span>
              </div>
            </el-form-item>

            <el-form-item label="人设策略">
              <el-input
                v-model="formData.personaConfig"
                type="textarea"
                :rows="5"
                placeholder='JSON或自然语言均可，例如：专业外贸业务员，口语化，少量Emoji，本地化表达'
              />
            </el-form-item>

            <el-form-item label="状态">
              <el-radio-group v-model="formData.status">
                <el-radio-button :label="0">停用</el-radio-button>
                <el-radio-button :label="1">启用</el-radio-button>
              </el-radio-group>
            </el-form-item>

            <el-form-item>
              <el-button type="primary" :loading="saving" @click="handleSave">
                <Icon icon="ep:check" class="mr-5px" /> 保存配置
              </el-button>
              <el-button :loading="dispatching" @click="handleDispatch">
                <Icon icon="ep:video-play" class="mr-5px" /> 触发一次
              </el-button>
            </el-form-item>
          </el-form>
        </ContentWrap>
      </el-col>

      <el-col :span="14">
        <ContentWrap>
          <div class="section-header">
            <div>
              <div class="section-title">触达记录</div>
              <div class="section-subtitle">记录AI评论/私信的生成内容、发送状态和判断理由</div>
            </div>
            <el-button @click="getTouchRecordList">
              <Icon icon="ep:refresh" class="mr-5px" /> 刷新
            </el-button>
          </div>

          <el-form class="search-form mt-16px" :model="recordQuery" :inline="true" label-width="72px">
            <el-form-item label="触达类型">
              <el-select v-model="recordQuery.touchType" clearable class="!w-140px">
                <el-option label="评论" value="comment" />
                <el-option label="私信" value="dm" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="recordQuery.status" clearable class="!w-140px">
                <el-option label="待发送" :value="0" />
                <el-option label="发送中" :value="1" />
                <el-option label="成功" :value="2" />
                <el-option label="失败" :value="3" />
                <el-option label="跳过" :value="4" />
              </el-select>
            </el-form-item>
            <el-form-item>
              <el-button @click="handleRecordQuery">
                <Icon icon="ep:search" class="mr-5px" /> 搜索
              </el-button>
            </el-form-item>
          </el-form>

          <el-table
            v-loading="recordLoading"
            :data="recordList"
            :stripe="true"
            :show-overflow-tooltip="true"
          >
            <el-table-column label="ID" prop="id" width="80" />
            <el-table-column label="类型" prop="touchType" width="90">
              <template #default="scope">
                <el-tag>{{ scope.row.touchType === 'dm' ? '私信' : '评论' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="状态" prop="status" width="90">
              <template #default="scope">
                <el-tag :type="getRecordStatusType(scope.row.status)">
                  {{ getRecordStatusLabel(scope.row.status) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="目标" prop="targetUrl" min-width="180" show-overflow-tooltip>
              <template #default="scope">
                <el-link v-if="scope.row.targetUrl" :href="scope.row.targetUrl" target="_blank">
                  {{ scope.row.targetUrl }}
                </el-link>
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column label="账号" prop="fbAccount" width="150" />
            <el-table-column label="内容" prop="generatedContent" min-width="220" show-overflow-tooltip />
            <el-table-column label="AI理由" prop="aiReason" min-width="180" show-overflow-tooltip />
            <el-table-column label="失败原因" prop="failReason" min-width="160" show-overflow-tooltip />
            <el-table-column label="创建时间" prop="createTime" width="160">
              <template #default="scope">{{ formatDateTime(scope.row.createTime) }}</template>
            </el-table-column>
          </el-table>

          <Pagination
            :total="recordTotal"
            v-model:page="recordQuery.pageNo"
            v-model:limit="recordQuery.pageSize"
            @pagination="getTouchRecordList"
          />
        </ContentWrap>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts" name="FacebookAiAgent">
import ContentWrap from '@/components/ContentWrap/src/ContentWrap.vue'
import { dateFormatter } from '@/utils/formatTime'
import { FbAiAgentApi, type FbAiAgentConfig, type FbAiTouchRecord } from '@/api/facebook/aiagent'
import { KnowledgeApi, type KnowledgeVO } from '@/api/ai/knowledge/knowledge'
import { FbAccountApi, type FbAccount } from '@/api/facebook/account'
import * as WorkflowApi from '@/api/ai/workflow'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()
const formRef = ref()
const saving = ref(false)
const dispatching = ref(false)
const recordLoading = ref(false)
const knowledgeList = ref<KnowledgeVO[]>([])
const accountList = ref<FbAccount[]>([])
const workflowList = ref<any[]>([])
const recordList = ref<FbAiTouchRecord[]>([])
const recordTotal = ref(0)

const formData = reactive<FbAiAgentConfig>({
  agentName: '外贸获客AI业务员',
  knowledgeIds: '',
  seedKeywords: '[]',
  targetCountries: '[]',
  targetLanguages: '[]',
  accountIds: '',
  monitorGroupIds: '',
  leadScoreWorkflowId: undefined,
  commentWorkflowId: undefined,
  dmWorkflowId: undefined,
  autoCommentEnabled: true,
  autoDmEnabled: true,
  dailyCommentLimit: 50,
  dailyDmLimit: 30,
  replyDelayRange: '[180,600]',
  personaConfig: '',
  status: 0
})

const formState = reactive({
  seedKeywordList: [] as string[],
  targetCountryList: [] as string[],
  targetLanguageList: [] as string[],
  knowledgeIdList: [] as number[],
  accountIdList: [] as number[],
  monitorGroupList: [] as string[],
  replyDelayMin: 180,
  replyDelayMax: 600
})

const rules = {
  agentName: [{ required: true, message: '请输入Agent名称', trigger: 'blur' }]
}

const recordQuery = reactive({
  pageNo: 1,
  pageSize: 10,
  touchType: undefined as string | undefined,
  status: undefined as number | undefined
})

const parseJsonArray = <T,>(value?: string, fallback: T[] = []) => {
  if (!value) return fallback
  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed : fallback
  } catch {
    return fallback
  }
}

const splitIds = (value?: string) => {
  if (!value) return []
  return value
    .split(',')
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isFinite(item))
}

const syncConfigToState = (config?: FbAiAgentConfig) => {
  if (!config) return
  Object.assign(formData, config)
  formState.seedKeywordList = parseJsonArray<string>(config.seedKeywords)
  formState.targetCountryList = parseJsonArray<string>(config.targetCountries)
  formState.targetLanguageList = parseJsonArray<string>(config.targetLanguages)
  formState.knowledgeIdList = splitIds(config.knowledgeIds)
  formState.accountIdList = splitIds(config.accountIds)
  formState.monitorGroupList = (config.monitorGroupIds || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
  const delayRange = parseJsonArray<number>(config.replyDelayRange, [180, 600])
  formState.replyDelayMin = delayRange[0] ?? 180
  formState.replyDelayMax = delayRange[1] ?? 600
}

const buildSubmitData = (): FbAiAgentConfig => {
  return {
    ...formData,
    seedKeywords: JSON.stringify(formState.seedKeywordList),
    targetCountries: JSON.stringify(formState.targetCountryList),
    targetLanguages: JSON.stringify(formState.targetLanguageList),
    knowledgeIds: formState.knowledgeIdList.join(','),
    accountIds: formState.accountIdList.join(','),
    monitorGroupIds: formState.monitorGroupList.join(','),
    replyDelayRange: JSON.stringify([formState.replyDelayMin, formState.replyDelayMax])
  }
}

const validateBeforeSave = () => {
  if (formData.status !== 1) return true
  if (formState.seedKeywordList.length < 3 || formState.seedKeywordList.length > 5) {
    message.warning('启用Agent时，关键词种子建议配置3-5个')
    return false
  }
  if (!formState.knowledgeIdList.length) {
    message.warning('启用Agent前请选择产品知识库')
    return false
  }
  if (!formState.accountIdList.length) {
    message.warning('启用Agent前请选择执行账号池')
    return false
  }
  if (!formData.autoCommentEnabled && !formData.autoDmEnabled) {
    message.warning('启用Agent前至少开启自动评论或自动私信')
    return false
  }
  if (formState.replyDelayMax < formState.replyDelayMin) {
    message.warning('随机延迟结束值不能小于开始值')
    return false
  }
  return true
}

const getConfig = async () => {
  const data = await FbAiAgentApi.getConfig()
  syncConfigToState(data)
}

const getBaseOptions = async () => {
  const [knowledgeData, accountData, workflowData] = await Promise.all([
    KnowledgeApi.getSimpleKnowledgeList(),
    FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 }),
    WorkflowApi.getWorkflowPage({ pageNo: 1, pageSize: 200, status: 1 })
  ])
  knowledgeList.value = knowledgeData || []
  accountList.value = accountData?.list || []
  workflowList.value = workflowData?.list || []
}

const handleSave = async () => {
  await formRef.value?.validate()
  if (!validateBeforeSave()) return
  saving.value = true
  try {
    await FbAiAgentApi.saveConfig(buildSubmitData())
    message.success('保存成功')
    await getConfig()
  } finally {
    saving.value = false
  }
}

const handleDispatch = async () => {
  dispatching.value = true
  try {
    const result = await FbAiAgentApi.dispatchOnce()
    result.dispatched ? message.success(result.message) : message.warning(result.message)
  } finally {
    dispatching.value = false
  }
}

const getTouchRecordList = async () => {
  recordLoading.value = true
  try {
    const data = await FbAiAgentApi.getTouchRecordPage(recordQuery)
    recordList.value = data.list
    recordTotal.value = data.total
  } finally {
    recordLoading.value = false
  }
}

const handleRecordQuery = () => {
  recordQuery.pageNo = 1
  getTouchRecordList()
}

const formatDateTime = (date: any) => {
  if (!date) return '-'
  return dateFormatter(date)
}

const getRecordStatusLabel = (status?: number) => {
  const map: Record<number, string> = {
    0: '待发送',
    1: '发送中',
    2: '成功',
    3: '失败',
    4: '跳过'
  }
  return status !== undefined ? map[status] || String(status) : '-'
}

const getRecordStatusType = (status?: number) => {
  const map: Record<number, 'success' | 'warning' | 'info' | 'danger'> = {
    0: 'info',
    1: 'warning',
    2: 'success',
    3: 'danger',
    4: 'info'
  }
  return status !== undefined ? map[status] || 'info' : 'info'
}

onMounted(async () => {
  await getBaseOptions()
  await getConfig()
  await getTouchRecordList()
})
</script>

<style scoped lang="scss">
.agent-page {
  .section-header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
  }

  .section-title {
    font-size: 16px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .section-subtitle {
    margin-top: 4px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
  }

  .switch-row,
  .limit-row {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
  }

  .search-form {
    :deep(.el-form-item) {
      margin-bottom: 12px;
    }
  }
}
</style>
