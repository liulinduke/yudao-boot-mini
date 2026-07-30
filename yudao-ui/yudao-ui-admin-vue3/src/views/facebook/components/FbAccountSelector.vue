<template>
  <el-popover
    v-model:visible="visible"
    placement="bottom-start"
    :width="360"
    trigger="click"
    popper-class="fb-account-selector-popper"
  >
    <template #reference>
      <div class="account-selector-trigger" :class="{ 'is-focus': visible }">
        <div v-if="selectedAccounts.length" class="account-selector-tags">
          <el-tag
            v-for="account in selectedAccounts.slice(0, 2)"
            :key="String(account.id)"
            size="small"
            closable
            @close.stop="toggleAccount(account.id, false)"
          >
            {{ account.fbAccount || account.id }}
          </el-tag>
          <span v-if="selectedAccounts.length > 2" class="account-selector-more">
            +{{ selectedAccounts.length - 2 }} 个
          </span>
        </div>
        <span v-else class="account-selector-placeholder">{{ placeholder }}</span>
        <Icon icon="ep:arrow-down" :size="14" class="account-selector-arrow" />
      </div>
    </template>

    <div class="account-selector-panel">
      <el-input v-model="keyword" clearable placeholder="搜索账号或分组" class="mb-10px">
        <template #prefix><Icon icon="ep:search" /></template>
      </el-input>

      <div v-loading="loading" class="account-selector-list">
        <div v-for="group in filteredGroups" :key="`group-${group.id}`" class="account-group">
          <div class="account-group-header">
            <el-checkbox
              :model-value="isGroupChecked(group.id)"
              :indeterminate="isGroupIndeterminate(group.id)"
              @change="handleGroupChange(group.id, $event)"
            >
              <span class="account-group-name">{{ group.groupName }}</span>
              <span class="account-group-count">{{ group.accounts.length }}</span>
            </el-checkbox>
          </div>
          <div class="account-group-accounts">
            <el-checkbox
              v-for="account in group.accounts"
              :key="String(account.id)"
              :model-value="isSelected(account.id)"
              @change="handleAccountChange(account.id, $event)"
            >
              {{ account.fbAccount || account.id }}
            </el-checkbox>
          </div>
        </div>

        <div v-if="filteredUngroupedAccounts.length" class="account-group">
          <div class="account-group-header">
            <span class="account-group-name">未分组</span>
            <span class="account-group-count">{{ filteredUngroupedAccounts.length }}</span>
          </div>
          <div class="account-group-accounts">
            <el-checkbox
              v-for="account in filteredUngroupedAccounts"
              :key="String(account.id)"
              :model-value="isSelected(account.id)"
              @change="handleAccountChange(account.id, $event)"
            >
              {{ account.fbAccount || account.id }}
            </el-checkbox>
          </div>
        </div>

        <el-empty v-if="!loading && !filteredGroups.length && !filteredUngroupedAccounts.length" :image-size="56" description="暂无可用账号" />
      </div>

      <div class="account-selector-footer">
        <span>已选择 {{ selectedIds.length }} 个账号</span>
        <el-button link type="primary" @click="clearSelection">清空</el-button>
      </div>
    </div>
  </el-popover>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { FbAccountApi, filterSelectableFbAccounts, type FbAccount } from '@/api/facebook/account'

defineOptions({ name: 'FbAccountSelector' })

const props = withDefaults(
  defineProps<{
    modelValue: Array<string | number>
    placeholder?: string
  }>(),
  { placeholder: '请选择执行账号' }
)

const emit = defineEmits<{
  (event: 'update:modelValue', value: Array<string | number>): void
}>()

type AccountGroup = {
  id: string | number
  groupName: string
  accounts: FbAccount[]
}

const visible = ref(false)
const loading = ref(false)
const keyword = ref('')
const accounts = ref<FbAccount[]>([])
const groups = ref<Array<{ id: string | number; groupName: string }>>([])

const selectedIds = computed(() => props.modelValue.map((id) => String(id)))
const selectedAccounts = computed(() =>
  accounts.value.filter((account) => selectedIds.value.includes(String(account.id)))
)

const groupData = computed<AccountGroup[]>(() =>
  groups.value
    .map((group) => ({
      ...group,
      accounts: accounts.value.filter((account) => String(account.groupId) === String(group.id))
    }))
    .filter((group) => group.accounts.length)
)

const matchesKeyword = (value: unknown) =>
  !keyword.value || String(value || '').toLowerCase().includes(keyword.value.trim().toLowerCase())

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
  accounts.value.filter(
    (account) =>
      (!account.groupId ||
        !groups.value.some((group) => String(group.id) === String(account.groupId))) &&
      matchesKeyword(account.fbAccount)
  )
)

const isSelected = (id: string | number) => selectedIds.value.includes(String(id))

const getGroupAccountIds = (groupId: string | number) =>
  accounts.value
    .filter((account) => String(account.groupId) === String(groupId))
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

const loadOptions = async () => {
  loading.value = true
  try {
    const [groupDataResponse, accountDataResponse] = await Promise.all([
      AccountGroupApi.getAllEnabledGroups(),
      FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 2000 })
    ])
    groups.value = groupDataResponse || []
    accounts.value = filterSelectableFbAccounts(accountDataResponse?.list || [])
  } finally {
    loading.value = false
  }
}

onMounted(loadOptions)
</script>

<style scoped>
.account-selector-trigger {
  display: flex;
  align-items: center;
  width: 280px;
  max-width: 100%;
  height: 32px;
  min-height: 32px;
  box-sizing: border-box;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  background: var(--el-fill-color-blank);
  cursor: pointer;
}

.account-selector-trigger.is-focus,
.account-selector-trigger:hover {
  border-color: var(--el-color-primary);
}

.account-selector-tags {
  display: flex;
  flex: 1;
  align-items: center;
  min-width: 0;
  gap: 4px;
  overflow: hidden;
}

.account-selector-more,
.account-selector-placeholder {
  color: var(--el-text-color-placeholder);
  font-size: 13px;
}

.account-selector-arrow {
  flex: 0 0 auto;
  margin-left: auto;
  color: var(--el-text-color-placeholder);
}

.account-selector-panel {
  width: 100%;
}

.account-selector-list {
  max-height: 330px;
  overflow-y: auto;
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
  grid-template-columns: repeat(2, minmax(0, 1fr));
  padding: 4px 8px 0 24px;
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
