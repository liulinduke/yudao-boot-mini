<template>
  <div class="ai-agent-page">
    <el-row :gutter="12">
      <el-col :span="8">
        <ContentWrap>
          <div class="panel-title">创建Agent</div>
          <div class="panel-subtitle">先做 AI主页获客，其余入口先保留占位</div>
          <div class="agent-entry-list">
            <div
              v-for="item in agentEntries"
              :key="item.type"
              class="agent-entry"
              :class="{ disabled: item.disabled }"
              @click="openCreateWizard(item)"
            >
              <div class="agent-entry__header">
                <Icon :icon="item.icon" :size="18" />
                <span>{{ item.title }}</span>
              </div>
              <div class="agent-entry__desc">{{ item.description }}</div>
              <el-tag size="small" :type="item.disabled ? 'info' : 'success'">
                {{ item.disabled ? '开发中' : '可创建' }}
              </el-tag>
            </div>
          </div>
        </ContentWrap>
      </el-col>

      <el-col :span="16">
        <ContentWrap>
          <div class="list-header">
            <div>
              <div class="panel-title">Agent列表</div>
              <div class="panel-subtitle">主页获客 Agent 的创建、运行、暂停与查看</div>
            </div>
            <el-button :loading="dispatching" @click="handleDispatch">
              <Icon icon="ep:video-play" class="mr-5px" /> 触发一次
            </el-button>
          </div>

          <el-form :model="queryParams" inline class="search-form">
            <el-form-item label="名称">
              <el-input v-model="queryParams.agentName" clearable class="!w-180px" />
            </el-form-item>
            <el-form-item label="类型">
              <el-select v-model="queryParams.agentType" clearable class="!w-150px">
                <el-option label="AI主页获客" value="page_lead" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="queryParams.status" clearable class="!w-130px">
                <el-option label="草稿" :value="0" />
                <el-option label="运行中" :value="1" />
                <el-option label="暂停" :value="2" />
                <el-option label="停止" :value="3" />
              </el-select>
            </el-form-item>
            <el-form-item>
              <el-button @click="handleQuery">
                <Icon icon="ep:search" class="mr-5px" /> 搜索
              </el-button>
            </el-form-item>
          </el-form>

          <el-table v-loading="loading" :data="list" :show-overflow-tooltip="true">
            <el-table-column label="Agent名称" prop="agentName" min-width="180" />
            <el-table-column label="类型" width="130">
              <template #default>AI主页获客</template>
            </el-table-column>
            <el-table-column label="状态" width="110">
              <template #default="scope">
                <el-tag :type="getStatusTagType(scope.row.status)">
                  {{ getStatusLabel(scope.row.status) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="线索" width="90">
              <template #default="scope">{{ scope.row.leadCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="待处理" width="90">
              <template #default="scope">{{ scope.row.pendingCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="关键词池" min-width="180">
              <template #default="scope">
                {{ parseJsonArray<string>(scope.row.keywordPool).slice(0, 4).join(' / ') || '-' }}
              </template>
            </el-table-column>
            <el-table-column label="执行时间" width="100">
              <template #default="scope">{{ scope.row.executeTime || '09:00' }}</template>
            </el-table-column>
            <el-table-column label="上次执行" width="160">
              <template #default="scope">{{ formatDateTime(scope.row.lastExecuteTime) }}</template>
            </el-table-column>
            <el-table-column label="创建时间" width="160">
              <template #default="scope">{{ formatDateTime(scope.row.createTime) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="260" fixed="right">
              <template #default="scope">
                <el-button link type="primary" @click="handleEdit(scope.row)">编辑</el-button>
                <el-button
                  v-if="scope.row.status === 1"
                  link
                  type="warning"
                  @click="handleStatus(scope.row, 2)"
                >
                  暂停
                </el-button>
                <el-button
                  v-else
                  link
                  type="success"
                  @click="handleStatus(scope.row, 1)"
                >
                  {{ scope.row.status === 0 ? '启动' : '恢复' }}
                </el-button>
                <el-button link type="primary" @click="handleView(scope.row)">详情</el-button>
                <el-button link type="danger" @click="handleDelete(scope.row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>

          <Pagination
            :total="total"
            v-model:page="queryParams.pageNo"
            v-model:limit="queryParams.pageSize"
            @pagination="getList"
          />
        </ContentWrap>
      </el-col>
    </el-row>

    <Dialog v-model="wizardVisible" :title="wizardTitle" width="980px">
      <el-steps :active="wizardStep" finish-status="success" class="mb-20px">
        <el-step title="客户来源" />
        <el-step title="发现策略" />
        <el-step title="触达策略" />
        <el-step title="AI业务员" />
      </el-steps>

      <el-form ref="wizardFormRef" :model="wizardForm" label-width="110px" :rules="wizardRules">
        <template v-if="wizardStep === 0">
          <el-form-item label="Agent名称" prop="agentName">
            <el-input v-model="wizardForm.agentName" placeholder="例如：美国卫浴客户开发" />
          </el-form-item>
          <el-form-item label="搜索方式" prop="searchMode">
            <el-radio-group v-model="wizardForm.searchMode">
              <el-radio-button label="keyword">关键词搜索</el-radio-button>
              <el-radio-button label="link">链接搜索</el-radio-button>
            </el-radio-group>
          </el-form-item>
          <el-form-item label="主营/出口产品" prop="exportProduct">
            <el-input v-model="wizardForm.exportProduct" placeholder="例如：Bathroom Faucet / Auto Parts" />
          </el-form-item>
          <el-form-item label="账号池" prop="accountIds">
            <el-select v-model="wizardState.accountIdList" multiple filterable class="w-full">
              <el-option
                v-for="item in accountList"
                :key="item.id"
                :label="item.fbAccount || String(item.id)"
                :value="item.id"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="目标国家">
            <el-select
              v-model="wizardState.targetCountryList"
              multiple
              filterable
              allow-create
              default-first-option
              class="w-full"
            >
              <el-option
                v-for="item in wizardState.targetCountryList"
                :key="item"
                :label="item"
                :value="item"
              />
            </el-select>
          </el-form-item>
        </template>

        <template v-else-if="wizardStep === 1">
          <el-form-item label="目标客户数量" prop="targetCustomerCount">
            <el-input-number v-model="wizardForm.targetCustomerCount" :min="1" :max="100000" />
          </el-form-item>
          <el-form-item label="执行频率">
            <el-select v-model="wizardForm.executeFrequency" class="!w-180px">
              <el-option label="每天" value="daily" />
            </el-select>
          </el-form-item>
          <el-form-item label="执行时间" prop="executeTime">
            <el-time-picker
              v-model="wizardForm.executeTime"
              format="HH:mm"
              value-format="HH:mm"
              placeholder="选择每天执行时间"
              class="!w-180px"
            />
          </el-form-item>
          <el-form-item label="种子关键词" prop="seedKeywords">
            <el-input
              v-model="wizardState.seedKeywordsText"
              type="textarea"
              :rows="5"
              placeholder="每行一个关键词，例如&#10;bathroom faucet&#10;bath faucet&#10;shower faucet"
            />
          </el-form-item>
          <el-form-item label="AI扩展关键词">
            <div class="inline-row">
              <el-checkbox v-model="wizardForm.aiKeywordExpandEnabled">AI扩展关键词</el-checkbox>
              <el-input-number
                v-model="wizardForm.aiKeywordExpandCount"
                :min="1"
                :max="100"
                :disabled="!wizardForm.aiKeywordExpandEnabled"
              />
              <el-button
                :disabled="!wizardForm.aiKeywordExpandEnabled"
                :loading="generatingKeywords"
                @click="handleGenerateKeywords"
              >
                生成关键词
              </el-button>
            </div>
          </el-form-item>
          <el-form-item label="关键词池" prop="keywordPool">
            <div class="keyword-pool">
              <el-tag
                v-for="(item, index) in wizardState.keywordPoolList"
                :key="`${item}-${index}`"
                closable
                class="keyword-tag"
                @close="removeKeyword(index)"
              >
                {{ item }}
              </el-tag>
            </div>
            <div class="inline-row mt-10px">
              <el-input
                v-model="wizardState.newKeyword"
                placeholder="新增关键词"
                class="!w-260px"
                @keyup.enter="appendKeyword"
              />
              <el-button @click="appendKeyword">新增</el-button>
            </div>
          </el-form-item>
          <el-form-item label="每轮执行关键词" prop="keywordsPerRun">
            <el-input-number
              v-model="wizardForm.keywordsPerRun"
              :min="1"
              :max="Math.max(wizardState.keywordPoolList.length || 1, 1)"
            />
          </el-form-item>
        </template>

        <template v-else-if="wizardStep === 2">
          <el-form-item label="自动评论">
            <el-switch v-model="wizardForm.autoCommentEnabled" />
          </el-form-item>
          <el-form-item label="自动私信">
            <el-switch v-model="wizardForm.autoDmEnabled" />
          </el-form-item>
          <el-form-item label="随机间隔">
            <div class="inline-row">
              <el-input-number v-model="wizardState.replyDelayMin" :min="0" :max="86400" />
              <span>至</span>
              <el-input-number v-model="wizardState.replyDelayMax" :min="0" :max="86400" />
              <span>秒</span>
            </div>
          </el-form-item>
          <el-form-item label="触达评分阈值" prop="touchScoreThreshold">
            <el-input-number v-model="wizardForm.touchScoreThreshold" :min="0" :max="100" />
          </el-form-item>
        </template>

        <template v-else>
          <el-form-item label="业务员人设" prop="personaType">
            <el-select v-model="wizardForm.personaType" class="!w-300px">
              <el-option label="专业外贸销售（推荐）" value="professional_sales" />
              <el-option label="顾问式销售" value="consultant_sales" />
              <el-option label="朋友式开发" value="friendly_sales" />
              <el-option label="强成交型销售" value="closer_sales" />
            </el-select>
          </el-form-item>
        </template>
      </el-form>

      <template #footer>
        <div class="dialog-footer">
          <el-button v-if="wizardStep > 0" @click="wizardStep--">上一步</el-button>
          <el-button v-if="wizardStep < 3" type="primary" @click="nextStep">下一步</el-button>
          <el-button v-else type="primary" :loading="saving" @click="submitWizard">保存Agent</el-button>
        </div>
      </template>
    </Dialog>

    <el-drawer v-model="detailVisible" title="Agent详情" size="70%">
      <template v-if="detailAgent">
        <div class="detail-head">
          <div>
            <div class="detail-title">{{ detailAgent.agentName }}</div>
            <div class="panel-subtitle">AI主页获客</div>
          </div>
          <el-tag :type="getStatusTagType(detailAgent.status)">
            {{ getStatusLabel(detailAgent.status) }}
          </el-tag>
        </div>

        <el-tabs v-model="detailTab">
          <el-tab-pane label="客户发现" name="discovery">
            <el-table v-loading="discoveryLoading" :data="discoveryList" :show-overflow-tooltip="true">
              <el-table-column label="发现时间" width="160">
                <template #default="scope">{{ formatDateTime(scope.row.createTime) }}</template>
              </el-table-column>
              <el-table-column label="关键词" prop="keyword" min-width="150" />
              <el-table-column label="发现来源" width="100">
                <template #default>主页</template>
              </el-table-column>
              <el-table-column label="发现客户数" prop="discoveredCount" width="110" />
              <el-table-column label="高意向客户数" prop="highIntentCount" width="120" />
              <el-table-column label="主页采集" prop="pageCollectCount" width="100" />
              <el-table-column label="AI分析" prop="aiAnalyzeCount" width="90" />
              <el-table-column label="过滤" prop="filteredCount" width="90" />
              <el-table-column label="最终线索" prop="finalLeadCount" width="100" />
            </el-table>
          </el-tab-pane>

          <el-tab-pane label="线索列表" name="leads">
            <el-table v-loading="leadLoading" :data="leadList" :show-overflow-tooltip="true">
              <el-table-column label="客户名称" prop="userName" min-width="160" />
              <el-table-column label="国家" prop="country" width="110" />
              <el-table-column label="客户类型" prop="leadType" width="130" />
              <el-table-column label="评分" prop="productRelevanceScore" width="90" />
              <el-table-column label="联系方式" width="200">
                <template #default="scope">
                  {{ scope.row.whatsapp || scope.row.email || scope.row.phonenumber || '-' }}
                </template>
              </el-table-column>
              <el-table-column label="最近活跃" width="160">
                <template #default="scope">{{ formatDateTime(scope.row.lastPostTime) }}</template>
              </el-table-column>
              <el-table-column label="状态" prop="touchStatus" width="110" />
              <el-table-column label="AI分析原因" prop="intentReason" min-width="220" />
            </el-table>
          </el-tab-pane>

          <el-tab-pane label="触达记录" name="touches">
            <el-table v-loading="touchLoading" :data="touchList" :show-overflow-tooltip="true">
              <el-table-column label="客户" prop="targetUrl" min-width="220" />
              <el-table-column label="方式" width="90">
                <template #default="scope">{{ scope.row.touchType === 'dm' ? '私信' : '评论' }}</template>
              </el-table-column>
              <el-table-column label="触达账号" prop="fbAccount" width="140" />
              <el-table-column label="状态" width="100">
                <template #default="scope">
                  <el-tag :type="getTouchStatusTagType(scope.row.status)">
                    {{ getTouchStatusLabel(scope.row.status) }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column label="发送内容" prop="generatedContent" min-width="240" />
              <el-table-column label="时间" width="160">
                <template #default="scope">{{ formatDateTime(scope.row.createTime) }}</template>
              </el-table-column>
            </el-table>
          </el-tab-pane>

          <el-tab-pane label="运行日志" name="logs">
            <div v-loading="runLogLoading" class="timeline-box">
              <div v-for="item in runLogList" :key="item.id" class="timeline-item">
                <div class="timeline-time">{{ formatDateTime(item.createTime) }}</div>
                <div class="timeline-content">
                  <div class="timeline-title">{{ item.title }}</div>
                  <div class="timeline-desc">{{ item.content }}</div>
                </div>
              </div>
            </div>
          </el-tab-pane>
        </el-tabs>
      </template>
    </el-drawer>
  </div>
</template>

<script setup lang="ts" name="FbAiAgent">
defineOptions({ name: 'FbAiAgent' })
import { dateFormatter } from '@/utils/formatTime'
import ContentWrap from '@/components/ContentWrap/src/ContentWrap.vue'
import { useMessage } from '@/hooks/web/useMessage'
import { FbAccountApi, type FbAccount } from '@/api/facebook/account'
import {
  FbAiAgentApi,
  type FbAiAgentConfig,
  type FbAiAgentDiscoveryLog,
  type FbAiAgentRunLog,
  type FbAiTouchRecord
} from '@/api/facebook/aiagent'
import { startBrowserCollect } from '@/utils/wpfBridge'

const message = useMessage()

const loading = ref(false)
const saving = ref(false)
const dispatching = ref(false)
const generatingKeywords = ref(false)
const wizardVisible = ref(false)
const wizardTitle = ref('创建AI主页获客Agent')
const wizardStep = ref(0)
const wizardFormRef = ref()
const detailVisible = ref(false)
const detailTab = ref('discovery')

const discoveryLoading = ref(false)
const leadLoading = ref(false)
const touchLoading = ref(false)
const runLogLoading = ref(false)

const list = ref<FbAiAgentConfig[]>([])
const total = ref(0)
const accountList = ref<FbAccount[]>([])
const detailAgent = ref<FbAiAgentConfig>()
const discoveryList = ref<FbAiAgentDiscoveryLog[]>([])
const leadList = ref<any[]>([])
const touchList = ref<FbAiTouchRecord[]>([])
const runLogList = ref<FbAiAgentRunLog[]>([])

const agentEntries = [
  {
    type: 'page_lead',
    title: 'AI主页获客',
    icon: 'ep:office-building',
    description: '关键词发现主页客户，深度采集后自动筛选和触达',
    disabled: false
  },
  {
    type: 'group_post',
    title: 'AI群帖获客',
    icon: 'ep:chat-line-round',
    description: '监控群帖发现潜在买家',
    disabled: true
  },
  {
    type: 'group_comment',
    title: 'AI群帖评论截流',
    icon: 'ep:comment',
    description: '围绕评论区高意向用户做自动截流',
    disabled: true
  },
  {
    type: 'competitor_buyer',
    title: 'AI竞品买家截流',
    icon: 'ep:trend-charts',
    description: '从竞品买家和互动用户中识别潜客',
    disabled: true
  }
]

const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  agentName: '',
  agentType: undefined as string | undefined,
  status: undefined as number | undefined
})

const wizardForm = reactive<FbAiAgentConfig>({
  id: undefined,
  agentName: '',
  agentType: 'page_lead',
  searchMode: 'keyword',
  exportProduct: '',
  accountIds: '',
  seedKeywords: '[]',
  keywordPool: '[]',
  keywordCursor: 0,
  keywordsPerRun: 5,
  aiKeywordExpandEnabled: true,
  aiKeywordExpandCount: 20,
  targetCustomerCount: 1000,
  executeFrequency: 'daily',
  executeTime: '09:00',
  targetCountries: '[]',
  autoCommentEnabled: true,
  autoDmEnabled: true,
  dailyCommentLimit: 50,
  dailyDmLimit: 30,
  touchScoreThreshold: 90,
  replyDelayRange: '[180,600]',
  personaType: 'professional_sales',
  personaConfig: '',
  status: 0
})

const wizardState = reactive({
  accountIdList: [] as number[],
  targetCountryList: [] as string[],
  seedKeywordsText: '',
  keywordPoolList: [] as string[],
  newKeyword: '',
  replyDelayMin: 180,
  replyDelayMax: 600
})

const wizardRules = {
  agentName: [{ required: true, message: '请输入Agent名称', trigger: 'blur' }],
  accountIds: [{ required: true, message: '请选择账号池', trigger: 'change' }]
}

const parseJsonArray = <T,>(value?: string, fallback: T[] = []) => {
  if (!value) return fallback
  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed : fallback
  } catch {
    return fallback
  }
}

const parseLines = (value: string) =>
  value
    .split('\n')
    .map((item) => item.trim())
    .filter(Boolean)

const formatDateTime = (value?: string | Date) => {
  if (!value) return '-'
  return dateFormatter(value)
}

const getStatusLabel = (status?: number) => {
  const map: Record<number, string> = { 0: '草稿', 1: '运行中', 2: '暂停', 3: '停止' }
  return status !== undefined ? map[status] || String(status) : '-'
}

const getStatusTagType = (status?: number) => {
  const map: Record<number, 'info' | 'success' | 'warning' | 'danger'> = {
    0: 'info',
    1: 'success',
    2: 'warning',
    3: 'danger'
  }
  return status !== undefined ? map[status] || 'info' : 'info'
}

const getTouchStatusLabel = (status?: number) => {
  const map: Record<number, string> = { 0: '待发送', 1: '发送中', 2: '成功', 3: '失败', 4: '跳过' }
  return status !== undefined ? map[status] || String(status) : '-'
}

const getTouchStatusTagType = (status?: number) => {
  const map: Record<number, 'info' | 'success' | 'warning' | 'danger'> = {
    0: 'info',
    1: 'warning',
    2: 'success',
    3: 'danger',
    4: 'info'
  }
  return status !== undefined ? map[status] || 'info' : 'info'
}

const syncWizard = (config?: FbAiAgentConfig) => {
  Object.assign(wizardForm, {
    id: undefined,
    agentName: '',
    agentType: 'page_lead',
    searchMode: 'keyword',
    exportProduct: '',
    accountIds: '',
    seedKeywords: '[]',
    keywordPool: '[]',
    keywordCursor: 0,
    keywordsPerRun: 5,
    aiKeywordExpandEnabled: true,
    aiKeywordExpandCount: 20,
    targetCustomerCount: 1000,
    executeFrequency: 'daily',
    executeTime: '09:00',
    targetCountries: '[]',
    autoCommentEnabled: true,
    autoDmEnabled: true,
    dailyCommentLimit: 50,
    dailyDmLimit: 30,
    touchScoreThreshold: 90,
    replyDelayRange: '[180,600]',
    personaType: 'professional_sales',
    personaConfig: '',
    status: 0,
    ...config
  })
  wizardState.accountIdList = (wizardForm.accountIds || '')
    .split(',')
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isFinite(item))
  wizardState.targetCountryList = parseJsonArray<string>(wizardForm.targetCountries)
  wizardState.keywordPoolList = parseJsonArray<string>(wizardForm.keywordPool)
  wizardState.seedKeywordsText = parseJsonArray<string>(wizardForm.seedKeywords).join('\n')
  const delayRange = parseJsonArray<number>(wizardForm.replyDelayRange, [180, 600])
  wizardState.replyDelayMin = delayRange[0] ?? 180
  wizardState.replyDelayMax = delayRange[1] ?? 600
  wizardState.newKeyword = ''
}

const buildSubmitData = (): FbAiAgentConfig => {
  const seedKeywords = parseLines(wizardState.seedKeywordsText)
  return {
    ...wizardForm,
    agentType: 'page_lead',
    accountIds: wizardState.accountIdList.join(','),
    targetCountries: JSON.stringify(wizardState.targetCountryList),
    seedKeywords: JSON.stringify(seedKeywords),
    keywordPool: JSON.stringify(wizardState.keywordPoolList),
    replyDelayRange: JSON.stringify([wizardState.replyDelayMin, wizardState.replyDelayMax])
  }
}

const validateWizard = () => {
  const seedKeywords = parseLines(wizardState.seedKeywordsText)
  if (!wizardState.accountIdList.length) {
    message.warning('请选择账号池')
    return false
  }
  if (!seedKeywords.length) {
    message.warning('请至少输入一个种子关键词')
    return false
  }
  if (!wizardForm.exportProduct?.trim()) {
    message.warning('请输入主营/出口产品')
    return false
  }
  if (!wizardState.keywordPoolList.length) {
    message.warning('关键词池不能为空')
    return false
  }
  if (!wizardForm.executeTime) {
    message.warning('请选择每天执行时间')
    return false
  }
  if ((wizardForm.keywordsPerRun || 1) > wizardState.keywordPoolList.length) {
    message.warning('每轮执行关键词数量不能大于关键词池总数')
    return false
  }
  if (wizardState.replyDelayMax < wizardState.replyDelayMin) {
    message.warning('随机间隔结束值不能小于开始值')
    return false
  }
  return true
}

const getBaseOptions = async () => {
  const accountData = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
  accountList.value = accountData?.list || []
}

const getList = async () => {
  loading.value = true
  try {
    const data = await FbAiAgentApi.getConfigPage(queryParams)
    list.value = data.list || []
    total.value = data.total || 0
  } finally {
    loading.value = false
  }
}

const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

const openCreateWizard = (item: any) => {
  if (item.disabled) {
    message.warning('这个入口下一版再接')
    return
  }
  wizardTitle.value = '创建AI主页获客Agent'
  wizardStep.value = 0
  syncWizard({ agentType: item.type, status: 0 })
  wizardVisible.value = true
}

const handleEdit = async (row: FbAiAgentConfig) => {
  const data = await FbAiAgentApi.getConfigById(Number(row.id))
  wizardTitle.value = '编辑AI主页获客Agent'
  wizardStep.value = 0
  syncWizard(data)
  wizardVisible.value = true
}

const handleStatus = async (row: FbAiAgentConfig, status: number) => {
  await FbAiAgentApi.updateStatus({ id: Number(row.id), status })
  message.success('状态已更新')
  await getList()
}

const handleDelete = async (row: FbAiAgentConfig) => {
  await message.delConfirm(`确认删除 Agent「${row.agentName}」吗？`)
  await FbAiAgentApi.deleteConfig(Number(row.id))
  message.success('删除成功')
  await getList()
}

const handleDispatch = async () => {
  dispatching.value = true
  try {
    const result = await FbAiAgentApi.dispatchOnce()
    result.dispatched ? message.success(result.message) : message.warning(result.message)
    const details = result.details || []
    details.forEach((detail) => {
      startBrowserCollect(
        String(detail.detailId),
        detail.fbAccount,
        detail.cookie || null,
        detail.searchUrl,
        detail.expectedCount || 1,
        detail.taskType || 1
      )
    })
    if (details.length > 0) {
      message.info(`已提交 ${details.length} 个采集明细到WPF浏览器`)
    }
    if (detailVisible.value) {
      await loadDetailTabs()
    }
  } finally {
    dispatching.value = false
  }
}

const nextStep = async () => {
  if (wizardStep.value === 0 && !wizardForm.agentName.trim()) {
    message.warning('请先输入Agent名称')
    return
  }
  if (wizardStep.value === 1 && !wizardState.keywordPoolList.length) {
    wizardState.keywordPoolList = parseLines(wizardState.seedKeywordsText)
  }
  if (wizardStep.value === 1 && !validateWizard()) {
    return
  }
  wizardStep.value++
}

const appendKeyword = () => {
  const value = wizardState.newKeyword.trim()
  if (!value) return
  if (!wizardState.keywordPoolList.includes(value)) {
    wizardState.keywordPoolList.push(value)
  }
  wizardState.newKeyword = ''
}

const removeKeyword = (index: number) => {
  wizardState.keywordPoolList.splice(index, 1)
}

const handleGenerateKeywords = async () => {
  const seedKeywords = parseLines(wizardState.seedKeywordsText)
  if (!seedKeywords.length) {
    message.warning('请先输入种子关键词')
    return
  }
  generatingKeywords.value = true
  try {
    const data = await FbAiAgentApi.generateKeywords({
      seedKeywords,
      targetCountries: wizardState.targetCountryList,
      expandCount: wizardForm.aiKeywordExpandCount
    })
    const merged = [...seedKeywords, ...(data.keywords || [])]
    wizardState.keywordPoolList = Array.from(new Set(merged))
    message.success(`已生成 ${data.keywords?.length || 0} 个关键词`)
  } finally {
    generatingKeywords.value = false
  }
}

const submitWizard = async () => {
  if (!validateWizard()) return
  saving.value = true
  try {
    const status = wizardForm.status === 0 ? 1 : wizardForm.status
    await FbAiAgentApi.saveConfig({ ...buildSubmitData(), status })
    message.success('Agent已保存')
    wizardVisible.value = false
    await getList()
  } finally {
    saving.value = false
  }
}

const handleView = async (row: FbAiAgentConfig) => {
  detailAgent.value = await FbAiAgentApi.getConfigById(Number(row.id))
  detailVisible.value = true
  detailTab.value = 'discovery'
  await loadDetailTabs()
}

const loadDetailTabs = async () => {
  if (!detailAgent.value?.id) return
  const agentConfigId = Number(detailAgent.value.id)
  discoveryLoading.value = true
  leadLoading.value = true
  touchLoading.value = true
  runLogLoading.value = true
  try {
    const [discoveryData, leadData, touchData, runLogData] = await Promise.all([
      FbAiAgentApi.getDiscoveryLogPage({ pageNo: 1, pageSize: 50, agentConfigId }),
      FbAiAgentApi.getLeadPage({ pageNo: 1, pageSize: 50, agentConfigId }),
      FbAiAgentApi.getTouchRecordPage({ pageNo: 1, pageSize: 50, agentConfigId }),
      FbAiAgentApi.getRunLogPage({ pageNo: 1, pageSize: 50, agentConfigId })
    ])
    discoveryList.value = discoveryData.list || []
    leadList.value = leadData.list || []
    touchList.value = touchData.list || []
    runLogList.value = runLogData.list || []
  } finally {
    discoveryLoading.value = false
    leadLoading.value = false
    touchLoading.value = false
    runLogLoading.value = false
  }
}

onMounted(async () => {
  await getBaseOptions()
  await getList()
})
</script>

<style scoped lang="scss">
.ai-agent-page {
  .panel-title {
    font-size: 16px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .panel-subtitle {
    margin-top: 4px;
    margin-bottom: 16px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
  }

  .agent-entry-list {
    display: grid;
    gap: 12px;
  }

  .agent-entry {
    padding: 14px;
    border: 1px solid var(--el-border-color);
    border-radius: 8px;
    cursor: pointer;
    transition: 0.2s ease;
    background: linear-gradient(135deg, #ffffff 0%, #f5f7fa 100%);
  }

  .agent-entry:hover {
    border-color: var(--el-color-primary);
    transform: translateY(-1px);
  }

  .agent-entry.disabled {
    opacity: 0.65;
    cursor: not-allowed;
  }

  .agent-entry__header {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 14px;
    font-weight: 600;
    margin-bottom: 8px;
  }

  .agent-entry__desc {
    min-height: 38px;
    margin-bottom: 10px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
    line-height: 1.5;
  }

  .list-header,
  .detail-head {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 12px;
    margin-bottom: 16px;
  }

  .inline-row {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
  }

  .keyword-pool {
    width: 100%;
    min-height: 42px;
    padding: 10px;
    border: 1px dashed var(--el-border-color);
    border-radius: 8px;
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    background: #fafafa;
  }

  .keyword-tag {
    margin-right: 0;
  }

  .detail-title {
    font-size: 18px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .timeline-box {
    display: grid;
    gap: 14px;
    padding-right: 8px;
  }

  .timeline-item {
    display: grid;
    grid-template-columns: 140px 1fr;
    gap: 14px;
    padding: 12px 14px;
    border-radius: 8px;
    background: #fafafa;
    border: 1px solid var(--el-border-color-lighter);
  }

  .timeline-time {
    font-size: 12px;
    color: var(--el-text-color-secondary);
  }

  .timeline-title {
    font-size: 14px;
    font-weight: 600;
    margin-bottom: 4px;
  }

  .timeline-desc {
    font-size: 13px;
    line-height: 1.6;
    color: var(--el-text-color-secondary);
  }
}
</style>
