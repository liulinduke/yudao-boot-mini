<template>
  <div>
    <div class="account-selector-action">
      <el-switch
        v-model="selectionMode"
        inline-prompt
        active-text="自动"
        inactive-text="手动"
        active-value="AUTO"
        inactive-value="MANUAL"
      />
      <span class="account-selector-mode-label">
        {{ selectionMode === 'AUTO' ? '程序自动分配' : '手动选择' }}
      </span>
      <span v-if="selectionMode === 'AUTO'" class="account-selector-mode-description">
        系统按使用情况平均分配
      </span>
      <el-button
        v-if="selectionMode === 'MANUAL'"
        type="primary"
        class="account-selector-open-button"
        @click="visible = true"
      >
        <Icon icon="ep:plus" :size="16" />
        <span>选择账号</span>
      </el-button>
      <span v-if="selectionMode === 'MANUAL'" class="account-selector-mode-label">
        已选择 {{ selectedAccounts.length }} 个
      </span>
    </div>

    <el-dialog
      v-model="visible"
      title="选择执行账号"
      width="680px"
      append-to-body
      :destroy-on-close="false"
      class="fb-account-selector-dialog"
    >
      <div class="account-selector-panel">
      <div class="account-selector-tip">
        系统会优先使用执行较少、较久未使用的账号，并尽量平均分摊任务。
      </div>
      <div v-if="selectionMode === 'AUTO'" class="account-selector-auto-summary">
        <div class="account-selector-auto-title">系统自动分配账号</div>
        <div>当前有 {{ accounts.length }} 个可用账号，系统会按照使用次数和最近执行时间自动平均分配。</div>
      </div>

      <template v-else>
        <el-input v-model="keyword" clearable placeholder="搜索账号或分组" class="mb-10px">
          <template #prefix><Icon icon="ep:search" /></template>
        </el-input>
      </template>

      <div
        v-if="selectionMode === 'MANUAL'"
        v-loading="loading"
        class="account-selector-list"
        @scroll.passive="handleListScroll"
      >
        <div v-for="group in filteredGroups" :key="`group-${group.id}`" class="account-group">
          <div class="account-group-header">
            <el-checkbox
              v-if="selectionMode === 'MANUAL'"
              :model-value="isGroupChecked(group.id)"
              :indeterminate="isGroupIndeterminate(group.id)"
              @change="handleGroupChange(group.id, $event)"
            >
              <span class="account-group-name">{{ group.groupName }}</span>
              <span class="account-group-count">{{ group.accounts.length }}</span>
            </el-checkbox>
            <div v-else class="account-group-auto-header">
              <span class="account-group-name">{{ group.groupName }}</span>
              <span class="account-group-count">{{ group.accounts.length }}</span>
            </div>
          </div>
          <div class="account-group-accounts">
            <template v-if="selectionMode === 'MANUAL'">
              <el-checkbox
                v-for="account in group.accounts"
                :key="String(account.id)"
                :model-value="isSelected(account.id)"
                :disabled="account.eligible === false"
                @change="handleAccountChange(account.id, $event)"
              >
                <span>{{ account.fbAccount || account.id }}</span>
                <span class="account-meta">{{ accountSummary(account) }}</span>
              </el-checkbox>
            </template>
            <template v-else>
              <div v-for="account in group.accounts" :key="`auto-${String(account.id)}`" class="account-auto-row">
              <span>{{ account.fbAccount || account.id }}</span>
              <span class="account-meta">{{ accountSummary(account) }}</span>
              </div>
            </template>
          </div>
        </div>

        <div v-if="filteredUngroupedAccounts.length" class="account-group">
          <div class="account-group-header">
            <span class="account-group-name">未分组</span>
            <span class="account-group-count">{{ filteredUngroupedAccounts.length }}</span>
          </div>
          <div class="account-group-accounts">
            <template v-if="selectionMode === 'MANUAL'">
              <el-checkbox
                v-for="account in filteredUngroupedAccounts"
                :key="String(account.id)"
                :model-value="isSelected(account.id)"
                :disabled="account.eligible === false"
                @change="handleAccountChange(account.id, $event)"
              >
                <span>{{ account.fbAccount || account.id }}</span>
                <span class="account-meta">{{ accountSummary(account) }}</span>
              </el-checkbox>
            </template>
            <template v-else>
              <div v-for="account in filteredUngroupedAccounts" :key="`auto-ungrouped-${String(account.id)}`" class="account-auto-row">
              <span>{{ account.fbAccount || account.id }}</span>
              <span class="account-meta">{{ accountSummary(account) }}</span>
              </div>
            </template>
          </div>
        </div>

        <el-empty v-if="!loading && !filteredGroups.length && !filteredUngroupedAccounts.length" :image-size="56" description="暂无可用账号" />
        <div v-if="hasMoreAccounts" class="account-selector-loading-more">继续滚动加载更多</div>
      </div>

        <div class="account-selector-footer">
          <span v-if="selectionMode === 'MANUAL'">已选择 {{ selectedIds.length }} 个账号</span>
          <span v-else>可用账号 {{ accounts.length }} 个</span>
          <el-button
            v-if="selectionMode === 'MANUAL'"
            link
            type="primary"
            @click="clearSelection"
          >
            清空
          </el-button>
        </div>
      </div>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" @click="visible = false">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { FbAccountApi, type FbAccountSelectorOption } from '@/api/facebook/account'

defineOptions({ name: 'FbAccountSelector' })

const props = withDefaults(defineProps<{
    modelValue: Array<string | number>
    placeholder?: string
    scene?: string
    actionTypes?: string[]
    targetCount?: number
    selectionMode?: 'AUTO' | 'MANUAL'
  }>(), {
    placeholder: '请选择执行账号',
    scene: 'collect',
    actionTypes: () => [],
    targetCount: 1,
    selectionMode: 'AUTO'
  })

const emit = defineEmits<{
  (event: 'update:modelValue', value: Array<string | number>): void
  (event: 'update:selectionMode', value: 'AUTO' | 'MANUAL'): void
}>()

type AccountGroup = {
  id: string | number
  groupName: string
  accounts: FbAccountSelectorOption[]
}

const visible = ref(false)
const loading = ref(false)
const keyword = ref('')
const accounts = ref<FbAccountSelectorOption[]>([])
const groups = ref<Array<{ id: string | number; groupName: string }>>([])
const selectionMode = ref<'AUTO' | 'MANUAL'>(props.selectionMode)
const visibleAccountCount = ref(50)
const ACCOUNT_PAGE_SIZE = 50

const selectedIds = computed(() => props.modelValue.map((id) => String(id)))
const selectedAccounts = computed(() =>
  accounts.value.filter((account) => selectedIds.value.includes(String(account.id)))
)

const matchesKeyword = (value: unknown) =>
  !keyword.value || String(value || '').toLowerCase().includes(keyword.value.trim().toLowerCase())

const filteredAccountPool = computed(() =>
  accounts.value.filter(
    (account) =>
      matchesKeyword(account.fbAccount) ||
      (account.groupId != null &&
        groups.value.some(
          (group) =>
            String(group.id) === String(account.groupId) && matchesKeyword(group.groupName)
        ))
  )
)

const visibleAccounts = computed(() =>
  filteredAccountPool.value.slice(0, visibleAccountCount.value)
)

const hasMoreAccounts = computed(
  () => visibleAccountCount.value < filteredAccountPool.value.length
)

const groupData = computed<AccountGroup[]>(() =>
  groups.value
    .map((group) => ({
      ...group,
      accounts: visibleAccounts.value.filter(
        (account) => String(account.groupId) === String(group.id)
      )
    }))
    .filter((group) => group.accounts.length)
)

const filteredGroups = computed(() =>
  groupData.value
    .map((group) => ({
      ...group,
      accounts: group.accounts.filter(
        (account) => matchesKeyword(group.groupName) || matchesKeyword(account.fbAccount)
      )
    }))
    .filter((group) => matchesKeyword(group.groupName) || group.accounts.length)
)

const filteredUngroupedAccounts = computed(() =>
  visibleAccounts.value.filter(
    (account) =>
      (!account.groupId ||
        !groups.value.some((group) => String(group.id) === String(account.groupId))) &&
      matchesKeyword(account.fbAccount)
  )
)

const isSelected = (id: string | number) => selectedIds.value.includes(String(id))

const getGroupAccountIds = (groupId: string | number) =>
  accounts.value
    .filter(
      (account) =>
        String(account.groupId) === String(groupId) && account.eligible !== false
    )
    .map((account) => String(account.id))

const isGroupChecked = (groupId: string | number) => {
  const ids = getGroupAccountIds(groupId)
  return ids.length > 0 && ids.every((id) => selectedIds.value.includes(id))
}

const isGroupIndeterminate = (groupId: string | number) => {
  const ids = getGroupAccountIds(groupId)
  const selectedCount = ids.filter((id) => selectedIds.value.includes(id)).length
  return selectedCount > 0 && selectedCount < ids.length
}

const updateSelection = (ids: string[]) => {
  const accountIdSet = new Set(accounts.value.map((account) => String(account.id)))
  emit(
    'update:modelValue',
    Array.from(new Set(ids)).filter((id) => accountIdSet.has(id))
  )
}

const toggleAccount = (id: string | number, checked: boolean) => {
  const next = new Set(selectedIds.value)
  if (checked) next.add(String(id))
  else next.delete(String(id))
  updateSelection(Array.from(next))
}

const toggleGroup = (groupId: string | number, checked: boolean) => {
  const next = new Set(selectedIds.value)
  for (const id of getGroupAccountIds(groupId)) {
    if (checked) next.add(id)
    else next.delete(id)
  }
  updateSelection(Array.from(next))
}

const handleGroupChange = (groupId: string | number, value: unknown) => {
  toggleGroup(groupId, Boolean(value))
}

const handleAccountChange = (accountId: string | number, value: unknown) => {
  toggleAccount(accountId, Boolean(value))
}

const clearSelection = () => updateSelection([])

const handleListScroll = (event: Event) => {
  const target = event.currentTarget as HTMLElement
  if (
    target.scrollTop + target.clientHeight >= target.scrollHeight - 24 &&
    hasMoreAccounts.value
  ) {
    visibleAccountCount.value += ACCOUNT_PAGE_SIZE
  }
}

const loadOptions = async () => {
  loading.value = true
  try {
    const [groupDataResponse, accountDataResponse] = await Promise.all([
      AccountGroupApi.getAllEnabledGroups(),
      FbAccountApi.getSelectorOptions({
        scene: props.scene,
        actionTypes: props.actionTypes,
        targetCount: props.targetCount
      })
    ])
    groups.value = groupDataResponse || []
    accounts.value = accountDataResponse || []
    visibleAccountCount.value = ACCOUNT_PAGE_SIZE
    if (selectionMode.value === 'AUTO') {
      updateSelection(
        accounts.value
          .filter((account) => account.eligible !== false)
          .map((account) => String(account.id))
      )
    } else {
      updateSelection(selectedIds.value)
    }
  } finally {
    loading.value = false
  }
}

const accountSummary = (account: FbAccountSelectorOption) => {
  const today = account.today || {}
  const limits = account.limits || {}
  const total = account.total || {}
  return [
    `今日：私信 ${today.dm || 0}/${limits.dm || 0} · 转帖 ${today.repost || 0}/${limits.repost || 0} · 加组 ${today.join_group || 0}/${limits.join_group || 0} · 评论 ${today.comment || 0}/${limits.comment || 0} · 关注 ${today.follow || 0}/${limits.follow || 0}`,
    `累计：任务 ${total.taskCount || 0} · 私信 ${total.dm || 0} · 转帖 ${total.repost || 0} · 加组 ${total.join_group || 0} · 评论 ${total.comment || 0} · 关注 ${total.follow || 0} · 采集 ${total.collect || 0} 条`
  ].join('\n')
}

watch(selectionMode, (value) => {
  emit('update:selectionMode', value)
  if (value === 'AUTO') {
    updateSelection(accounts.value.filter((account) => account.eligible !== false).map((account) => String(account.id)))
  } else {
    updateSelection([])
  }
})

watch(keyword, () => {
  visibleAccountCount.value = ACCOUNT_PAGE_SIZE
})

watch(() => props.selectionMode, (value) => {
  if (value && value !== selectionMode.value) selectionMode.value = value
})

onMounted(loadOptions)
</script>

<style scoped>
.account-selector-action {
  display: flex;
  align-items: center;
  gap: 10px;
}

.account-selector-open-button {
  width: 120px;
  height: 32px;
  min-height: 32px;
  gap: 6px;
}

.account-selector-mode-label {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  white-space: nowrap;
}

.account-selector-mode-description {
  color: var(--el-text-color-placeholder);
  font-size: 12px;
  white-space: nowrap;
}

.account-selector-more,
.account-selector-placeholder {
  color: var(--el-text-color-placeholder);
  font-size: 13px;
}

.account-selector-panel {
  width: 640px;
  max-width: calc(100vw - 32px);
}

.account-selector-tip {
  margin-bottom: 10px;
  padding: 7px 9px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: 4px;
  font-size: 12px;
  line-height: 1.5;
}

.account-group-auto-header {
  display: flex;
  align-items: center;
  padding: 0 4px;
}

.account-auto-row {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  padding: 4px 0;
  font-size: 12px;
}

.account-meta {
  display: block;
  color: var(--el-text-color-secondary);
  font-size: 11px;
  line-height: 1.5;
  white-space: pre-line;
}

.account-selector-list {
  height: 420px;
  overflow-y: auto;
}

.account-selector-auto-summary {
  padding: 16px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: 4px;
  font-size: 13px;
  line-height: 1.7;
}

.account-selector-auto-title {
  margin-bottom: 4px;
  color: var(--el-text-color-primary);
  font-weight: 600;
}

.account-selector-loading-more {
  padding: 10px 0;
  color: var(--el-text-color-placeholder);
  font-size: 12px;
  text-align: center;
}

.account-group {
  padding: 4px 0 8px;
}

.account-group-header {
  display: flex;
  align-items: center;
  min-height: 28px;
  padding: 0 4px;
  background: var(--el-fill-color-light);
}

.account-group-name {
  font-weight: 600;
}

.account-group-count {
  margin-left: 6px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.account-group-accounts {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  padding: 4px 8px 0 24px;
}

.account-group-accounts :deep(.el-checkbox) {
  width: 100%;
  min-height: 48px;
  margin-right: 0;
  align-items: flex-start;
}

.account-group-accounts :deep(.el-checkbox__label) {
  min-width: 0;
  overflow: hidden;
  white-space: normal;
}

.account-selector-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
