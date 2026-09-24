<template>
  <ContentWrap>
    <div class="search-header">
      <div>
        <h2>AI 全网企业获客</h2>
        <p>输入客户目标，AI 搜索企业并整理可用结果。</p>
      </div>
      <div
        ><el-button @click="$router.push('/ai-search/source')">管理渠道</el-button
        ><el-button type="primary" @click="openCreate">创建获客任务</el-button></div
      >
    </div>
    <el-table v-loading="loading" :data="tasks" row-key="id">
      <el-table-column prop="name" label="任务" min-width="220" />
      <el-table-column prop="userGoal" label="获客目标" min-width="300" show-overflow-tooltip />
      <el-table-column prop="targetCount" label="目标企业" width="100" />
      <el-table-column label="状态" width="110">
        <template #default="{ row }">{{ statusText(row.status) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="390">
        <template #default="{ row }"
          ><el-button type="primary" link @click.stop="openRunDetail(row)">运行详情</el-button
          ><el-button
            type="primary"
            v-if="row.status !== 'RUNNING'"
            link
            @click.stop="openEdit(row)"
            >编辑</el-button
          ><el-button
            v-if="['RUNNING', 'STOPPED', 'COMPLETED'].indexOf(row.status || '') === -1"
            link
            type="primary"
            @click.stop="start(row)"
            >开始搜索</el-button
          ><el-button v-if="row.status === 'RUNNING'" link @click.stop="changeStatus(row, 'PAUSED')"
            >暂停</el-button
          ><el-button
            v-if="row.status === 'RUNNING' || row.status === 'PAUSED'"
            link
            type="danger"
            @click.stop="changeStatus(row, 'STOPPED')"
            >停止</el-button
          ><el-button v-if="row.status === 'PAUSED'" link type="primary" @click.stop="start(row)"
            >继续运行</el-button
          ><el-button link type="danger" @click.stop="removeTask(row)">删除</el-button></template
        >
      </el-table-column>
    </el-table>
    <el-divider />
    <el-table v-if="selectedTask && rounds.length" :data="rounds" size="small" class="runs-table"
      ><el-table-column prop="roundNo" label="轮次" width="70" /><el-table-column
        prop="strategy"
        label="搜索策略"
        min-width="300"
        show-overflow-tooltip /><el-table-column
        prop="resultCount"
        label="结果"
        width="75" /><el-table-column
        prop="newCompanyCount"
        label="新增企业"
        width="95" /><el-table-column
        prop="qualifiedCompanyCount"
        label="合格企业"
        width="95" /><el-table-column
        prop="duplicateCount"
        label="重复"
        width="75" /><el-table-column prop="newSourceCount" label="新渠道" width="75"
    /></el-table>
    <el-dialog v-model="runDetailVisible" title="运行详情" width="1300px">
      <el-form :inline="true" :model="runQuery"
        ><el-form-item label="公司名称"
          ><el-input v-model="runQuery.companyName" clearable /></el-form-item
        ><el-form-item label="联系人"
          ><el-input v-model="runQuery.contactName" clearable /></el-form-item
        ><el-form-item label="国家/地区"
          ><el-input v-model="runQuery.country" clearable /></el-form-item
        ><el-form-item
          ><el-checkbox v-model="runQuery.hasEmail">有邮箱</el-checkbox
          ><el-checkbox v-model="runQuery.hasPhone">有电话</el-checkbox></el-form-item
        ><el-button type="primary" @click="loadRunContacts">筛选</el-button></el-form
      >
      <el-table :data="runContacts" v-loading="runLoading"
        ><el-table-column prop="companyName" label="公司名称" min-width="190" /><el-table-column
          prop="icpLevel"
          label="AI推荐等级"
          width="110" /><el-table-column
          prop="companySize"
          label="公司规模"
          width="120" /><el-table-column
          prop="companyCountry"
          label="国家/地区"
          width="110" /><el-table-column
          prop="companyIndustry"
          label="行业"
          width="130" /><el-table-column
          prop="companyWebsite"
          label="公司官网"
          min-width="210" /><el-table-column
          prop="name"
          label="联系人"
          width="130" /><el-table-column prop="jobTitle" label="职位" width="150" /><el-table-column
          prop="email"
          label="邮箱"
          min-width="200" /><el-table-column
          prop="phone"
          label="电话"
          width="140" /><el-table-column prop="linkedinUrl" label="社交媒体" min-width="180"
      /></el-table>
      <Pagination
        :total="runTotal"
        v-model:page="runQuery.pageNo"
        v-model:limit="runQuery.pageSize"
        @pagination="loadRunContacts"
      />
    </el-dialog>
    <el-dialog
      v-model="dialogVisible"
      :title="editingTaskId ? '编辑 AI 获客任务' : '创建 AI 获客任务'"
      width="720px"
      @closed="createStep = 0"
    >
      <el-steps :active="createStep" finish-status="success" simple>
        <el-step title="目标信息" /><el-step title="AI搜索扩展" /><el-step title="确认创建" />
      </el-steps>
      <el-form
        v-loading="expansionLoading"
        element-loading-text="AI 正在生成搜索扩展…"
        :model="form"
        label-width="115px"
        class="create-form"
      >
        <template v-if="createStep === 0">
          <el-form-item label="获客目标" required
            ><el-input
              v-model="form.userGoal"
              type="textarea"
              :rows="4"
              placeholder="例如：找100家德国LED工业照明进口商"
          /></el-form-item>
          <el-form-item label="我的企业/产品"
            ><el-input v-model="form.company" placeholder="例如：生产LED工业照明灯具"
          /></el-form-item>
          <el-form-item label="获客关键词"
            ><el-input v-model="form.keywords" placeholder="例如：LED lighting importer"
          /></el-form-item>
          <el-form-item label="国家/地区"
            ><el-input v-model="form.targetCountry" placeholder="例如：德国"
          /></el-form-item>
          <el-form-item label="客户类型"
            ><el-input v-model="form.customerType" placeholder="例如：进口商、分销商"
          /></el-form-item>
          <el-form-item label="HS Code"
            ><el-input v-model="form.hsCodes" placeholder="可选，用于后续贸易背景核验"
          /></el-form-item>
          <el-form-item label="目标企业数"
            ><el-input-number v-model="form.targetCount" :min="1" :max="100000"
          /></el-form-item>
          <div class="form-tip plain-tip"
            >国家/地区、客户类型、行业和搜索方向将由 AI 根据获客目标自动判断。</div
          >
        </template>
        <template v-else-if="createStep === 1">
          <el-form-item label="最佳匹配关键词"
            ><el-checkbox-group v-model="form.searchKeywords"
              ><el-checkbox
                v-for="item in expansion.keywords"
                :key="item"
                :label="item" /></el-checkbox-group
            ><el-input
              v-model="keywordInput"
              placeholder="添加关键词后回车"
              @keyup.enter="addKeyword"
            /><div class="form-tip inline-tip"
              >首轮最多选择 6 个，任务运行后 AI 会继续扩展。</div
            ></el-form-item
          >
          <el-form-item v-if="expansion.productTerms?.length" label="产品扩展词"
            ><div class="tag-list"
              ><el-tag v-for="item in expansion.productTerms" :key="item">{{ item }}</el-tag></div
            ></el-form-item
          >
          <el-form-item v-if="expansion.customerRoles?.length" label="客户角色"
            ><div class="tag-list"
              ><el-tag v-for="item in expansion.customerRoles" :key="item" type="success">{{
                item
              }}</el-tag></div
            ></el-form-item
          >
          <el-form-item v-if="expansion.localTerms?.length" label="当地语言"
            ><div class="tag-list"
              ><el-tag v-for="item in expansion.localTerms" :key="item" type="warning">{{
                item
              }}</el-tag></div
            ></el-form-item
          >
          <el-form-item label="应用行业 / 使用场景"
            ><el-checkbox-group v-model="form.searchScenes"
              ><el-checkbox
                v-for="item in expansion.scenes"
                :key="item"
                :label="item" /></el-checkbox-group
            ><div class="form-tip inline-tip"
              >首轮最多选择 4 个，仅展示 AI 判断有价值的场景。</div
            ></el-form-item
          >
          <el-form-item label="联系人发现"
            ><el-switch v-model="form.contactEnrichment" /><div class="form-tip"
              >自动发现企业的多个关键联系人。</div
            ></el-form-item
          >
          <el-form-item label="搜索周期"
            ><el-select v-model="form.scheduleType"
              ><el-option label="单次" value="ONCE" /><el-option
                label="每天"
                value="DAILY" /><el-option label="每周" value="WEEKLY" /><el-option
                label="每月"
                value="MONTHLY" /></el-select
            ><el-input-number
              v-if="form.scheduleType !== 'ONCE'"
              v-model="scheduleIntervalNumber"
              :min="1"
              :max="365"
              class="interval-number"
            /><span v-if="form.scheduleType !== 'ONCE'" class="interval-label"
              >个周期</span
            ></el-form-item
          >
        </template>
        <template v-else>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="获客目标">{{
              form.userGoal || '未填写'
            }}</el-descriptions-item>
            <el-descriptions-item label="目标企业数">{{ form.targetCount }}</el-descriptions-item>
            <el-descriptions-item label="基础信息"
              >AI 自动判断国家、客户类型和搜索方向</el-descriptions-item
            >
            <el-descriptions-item label="搜索扩展"
              >最多 6 个关键词，最多 4 个首轮行业/场景；运行中继续扩展</el-descriptions-item
            >
            <el-descriptions-item label="关键联系人">{{
              form.contactEnrichment ? '开启' : '关闭'
            }}</el-descriptions-item>
            <el-descriptions-item label="搜索周期">{{ scheduleText }}</el-descriptions-item>
          </el-descriptions>
          <p class="form-tip"
            >创建后可在任务详情查看企业、联系人、证据和搜索进度；触达请在触达中心单独配置。</p
          >
        </template>
      </el-form>
      <template #footer
        ><el-button :disabled="expansionLoading" @click="dialogVisible = false">取消</el-button
        ><el-button v-if="createStep > 0" :disabled="expansionLoading" @click="createStep--"
          >上一步</el-button
        ><el-button
          v-if="createStep < 2"
          type="primary"
          :loading="expansionLoading"
          @click="nextCreateStep"
          >{{ expansionLoading ? 'AI 生成中' : '下一步' }}</el-button
        ><el-button v-else type="primary" @click="create">{{
          editingTaskId ? '确认修改' : '开始 AI 获客'
        }}</el-button></template
      >
    </el-dialog>
    <el-drawer v-model="detailVisible" title="企业详情" size="620px"
      ><template v-if="detail"
        ><h3>{{ detail.company?.name }}</h3
        ><el-descriptions :column="2" border
          ><el-descriptions-item label="国家/地区">{{
            detail.company?.country || '未知'
          }}</el-descriptions-item
          ><el-descriptions-item label="客户类型">{{
            detail.company?.customerType || '未知'
          }}</el-descriptions-item
          ><el-descriptions-item label="AI推荐">{{
            detail.company?.icpLevel || '未知'
          }}</el-descriptions-item
          ><el-descriptions-item label="评分">{{
            detail.company?.icpScore ?? '未评分'
          }}</el-descriptions-item
          ><el-descriptions-item label="官网" :span="2">{{
            detail.company?.website || '无'
          }}</el-descriptions-item></el-descriptions
        ><p>{{ detail.company?.description }}</p
        ><el-divider /><h4>联系人</h4
        ><el-table :data="detail.contacts || []"
          ><el-table-column prop="name" label="姓名" /><el-table-column
            prop="jobTitle"
            label="职位" /><el-table-column prop="email" label="邮箱" /></el-table
        ><h4>企业证据</h4
        ><el-table :data="detail.evidences || []"
          ><el-table-column prop="url" label="来源" show-overflow-tooltip /><el-table-column
            prop="evidenceText"
            label="摘要"
            show-overflow-tooltip /></el-table
        ><h4>贸易背景</h4
        ><el-table :data="detail.tradeEvidences || []"
          ><el-table-column prop="hsCode" label="HS Code" width="100" /><el-table-column
            prop="product"
            label="产品" /><el-table-column
            prop="tradeType"
            label="类型"
            width="100" /><el-table-column
            prop="partnerCountry"
            label="合作国家"
            width="110" /><el-table-column
            prop="url"
            label="来源"
            show-overflow-tooltip /></el-table></template
    ></el-drawer>
  </ContentWrap>
</template>
<script setup lang="ts">
import {
  AiSearchApi,
  type AiSearchCompanyVO,
  type AiSearchTaskVO,
  type AiSearchCompanyDetailVO,
  type AiSearchRoundVO
} from '@/api/facebook/aiSearch'
const message = useMessage()
const loading = ref(false)
const tasks = ref<AiSearchTaskVO[]>([])
const rounds = ref<AiSearchRoundVO[]>([])
const selectedTask = ref<AiSearchTaskVO>()
const dialogVisible = ref(false)
const detailVisible = ref(false)
const detail = ref<AiSearchCompanyDetailVO>()
const tradeHsCode = ref('')
const tradeProduct = ref('')
const tradeResult = ref<{ status: string; note?: string }>()
const createStep = ref(0)
const runDetailVisible = ref(false)
const runLoading = ref(false)
const runContacts = ref<any[]>([])
const runTotal = ref(0)
const runTaskId = ref<any>()
const runQuery = reactive({
  pageNo: 1,
  pageSize: 20,
  companyName: '',
  contactName: '',
  country: '',
  icpLevel: '',
  hasEmail: false,
  hasPhone: false
})
const loadRunContacts = async () => {
  if (!runTaskId.value) return
  runLoading.value = true
  try {
    const page: any = await AiSearchApi.contacts(runTaskId.value, runQuery)
    runContacts.value = page.list || []
    runTotal.value = page.total || 0
  } finally {
    runLoading.value = false
  }
}
const openRunDetail = (row: any) => {
  runTaskId.value = row.id
  runQuery.pageNo = 1
  runDetailVisible.value = true
  loadRunContacts()
}
const removeTask = async (row: any) => {
  await message.confirm('确认删除该任务吗？任务结果会保留在客户资源库。')
  await AiSearchApi.delete(row.id)
  await load()
  message.success('任务已删除')
}
const editingTaskId = ref<string | number>()
const originalSearchTarget = ref('')
const form = ref<
  AiSearchTaskVO & {
    company?: string
    keywords?: string
    searchKeywords?: string[]
    searchScenes?: string[]
  }
>({
  userGoal: '',
  targetCount: 100,
  aiKeywordExpand: true,
  contactEnrichment: true,
  scheduleType: 'ONCE',
  scheduleInterval: '1',
  searchKeywords: [],
  searchScenes: []
})
const expansion = ref({
  keywords: [] as string[],
  scenes: [] as string[],
  productTerms: [] as string[],
  customerRoles: [] as string[],
  localTerms: [] as string[]
})
const keywordInput = ref('')
const expansionLoading = ref(false)
const scheduleIntervalNumber = computed({
  get: () => Number(form.value.scheduleInterval || 1),
  set: (value: number) => {
    form.value.scheduleInterval = String(value || 1)
  }
})
const scheduleText = computed(() =>
  form.value.scheduleType === 'ONCE'
    ? '单次'
    : `${form.value.scheduleInterval || 1}${form.value.scheduleType === 'WEEKLY' ? '周' : form.value.scheduleType === 'MONTHLY' ? '月' : '天'}一次`
)
const load = async () => {
  loading.value = true
  try {
    tasks.value = (await AiSearchApi.list()) as any
  } finally {
    loading.value = false
  }
}
const selectTask = async (row: AiSearchTaskVO) => {
  selectedTask.value = row
  try {
    rounds.value = (await AiSearchApi.rounds(row.id!)) as any
  } catch (e) {
    // ignore
  }
}
const openCreate = () => {
  editingTaskId.value = undefined
  form.value = {
    userGoal: '',
    company: '',
    keywords: '',
    targetCountry: '',
    customerType: '',
    hsCodes: '',
    targetCount: 100,
    aiKeywordExpand: true,
    contactEnrichment: true,
    scheduleType: 'ONCE',
    scheduleInterval: '1',
    searchKeywords: [],
    searchScenes: []
  }
  expansion.value = {
    keywords: [],
    scenes: [],
    productTerms: [],
    customerRoles: [],
    localTerms: []
  }
  createStep.value = 0
  dialogVisible.value = true
}
const openEdit = (row: AiSearchTaskVO) => {
  editingTaskId.value = row.id
  form.value = { ...row, searchKeywords: [], searchScenes: [] }
  originalSearchTarget.value = JSON.stringify([
    row.userGoal,
    row.targetCountry,
    row.customerType,
    (row as any).company,
    (row as any).keywords,
    row.hsCodes
  ])
  expansion.value = {
    keywords: [],
    scenes: [],
    productTerms: [],
    customerRoles: [],
    localTerms: []
  }
  createStep.value = 0
  dialogVisible.value = true
}
const addKeyword = () => {
  const value = keywordInput.value.trim()
  if (value && !expansion.value.keywords.includes(value) && expansion.value.keywords.length < 6) {
    expansion.value.keywords.push(value)
    form.value.searchKeywords?.push(value)
  }
  keywordInput.value = ''
}
const nextCreateStep = async () => {
  if (createStep.value === 0 && !form.value.userGoal.trim())
    return message.warning('请输入获客目标')
  const targetChanged =
    !editingTaskId.value ||
    originalSearchTarget.value !==
      JSON.stringify([
        form.value.userGoal,
        form.value.targetCountry,
        form.value.customerType,
        form.value.company,
        form.value.keywords,
        form.value.hsCodes
      ])
  if (createStep.value === 0 && targetChanged) {
    expansionLoading.value = true
    try {
      const result = await AiSearchApi.expand({
        userGoal: form.value.userGoal,
        company: form.value.company,
        keywords: form.value.keywords,
        targetCountry: form.value.targetCountry,
        customerType: form.value.customerType,
        hsCode: form.value.hsCodes
      })
      expansion.value = result as any
      form.value.searchKeywords = [...expansion.value.keywords]
      form.value.searchScenes = [...expansion.value.scenes]
    } catch (e) {
      message.error('AI 搜索扩展生成失败，请稍后重试')
      return
    } finally {
      expansionLoading.value = false
    }
  }
  createStep.value++
}
const create = async () => {
  if (!form.value.userGoal.trim()) return message.warning('请输入获客目标')
  if (editingTaskId.value) await AiSearchApi.update({ ...form.value, id: editingTaskId.value })
  else await AiSearchApi.create(form.value)
  dialogVisible.value = false
  message.success(editingTaskId.value ? '任务已更新' : '任务已创建')
  await load()
}
const start = async (row: AiSearchTaskVO) => {
  await AiSearchApi.start(row.id!)
  message.success('搜索任务已启动')
  await load()
  await selectTask(row)
}
const showCompany = async (row: AiSearchCompanyVO) => {
  detail.value = await AiSearchApi.companyDetail(row.id)
  detailVisible.value = true
}
const verifyTrade = async () => {
  if (!detail.value?.company?.id) return
  tradeResult.value = await AiSearchApi.tradeVerify(
    detail.value.company.id,
    tradeHsCode.value || undefined,
    tradeProduct.value || undefined
  )
  detail.value = await AiSearchApi.companyDetail(detail.value.company.id)
}
const verifyCompanyTrade = async (row: AiSearchCompanyVO) => {
  tradeResult.value = await AiSearchApi.tradeVerify(row.id)
  message.info(`贸易核验：${tradeResult.value.status}，${tradeResult.value.note || ''}`)
}
const statusText = (status?: string) => {
  const labels: Record<string, string> = {
    DRAFT: '草稿',
    RUNNING: '运行中',
    PAUSED: '已暂停',
    STOPPED: '已停止',
    COMPLETED: '已完成',
    FAILED: '失败'
  }
  return labels[status || ''] || status || '未知'
}
const changeStatus = async (row: AiSearchTaskVO, status: string) => {
  await AiSearchApi.status(row.id!, status)
  await load()
}
onMounted(load)
</script>
<style scoped>
.search-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.search-header h2 {
  margin: 0 0 6px;
}
.search-header p {
  margin: 0;
  color: var(--el-text-color-secondary);
}
.stats {
  margin: 0 0 16px 8px;
}
.runs-table {
  margin: 0 0 16px;
}
.create-form {
  margin-top: 24px;
}
.form-tip {
  margin: 0 0 12px 115px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  line-height: 1.5;
}
.plain-tip {
  margin-left: 0;
}
.extension-box {
  padding: 10px 12px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
  line-height: 1.6;
  border-radius: 4px;
}
.tag-list {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.interval-number {
  margin-left: 12px;
  width: 120px;
}
.interval-label {
  margin-left: 8px;
  color: var(--el-text-color-secondary);
}
</style>
