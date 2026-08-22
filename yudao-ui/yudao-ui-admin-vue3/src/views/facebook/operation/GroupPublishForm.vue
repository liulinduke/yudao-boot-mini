<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="900px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      v-loading="formLoading"
    >
      <el-form-item
        label="执行账号"
        :prop="formData.accountSelectionMode === 'MANUAL' ? 'accountIds' : undefined"
      >
        <FbAccountSelector
          v-model="formData.accountIds"
          v-model:selection-mode="formData.accountSelectionMode"
          v-model:auto-account-count="formData.autoAccountCount"
          :show-auto-count="true"
          :action-types="['group_post']"
          class="w-full"
        />
      </el-form-item>

      <el-form-item label="帖子内容" prop="postContent">
        <el-input
          v-model="formData.postContent"
          type="textarea"
          :rows="5"
          placeholder="请输入帖子内容"
          maxlength="5000"
          show-word-limit
        />
      </el-form-item>

      <el-form-item label="发布增强">
        <el-checkbox v-model="formData.randomizeImagesAndAppendEmoji">
          图片加噪点并在内容末尾追加随机表情
        </el-checkbox>
      </el-form-item>

      <el-form-item label="图片/视频" prop="mediaUrls">
        <div>
          <el-button type="primary" @click="handleSelectFiles">
            <el-icon><Plus /></el-icon>
            选择图片/视频
          </el-button>
          <el-button @click="clearFiles" v-if="formData.mediaUrls.length > 0"> 清空 </el-button>
        </div>
        <div v-if="formData.mediaUrls.length > 0" class="mt-2">
          <el-tag
            v-for="(path, index) in formData.mediaUrls"
            :key="index"
            closable
            @close="removeFile(index)"
            class="mr-2 mb-2"
          >
            {{ getFileName(path) }}
          </el-tag>
        </div>
        <div class="text-sm text-gray-500 mt-1 ml-2">最多选择10个图片或视频（本地路径）</div>
      </el-form-item>

      <el-form-item label="匿名发帖" prop="anonymouslyPost">
        <el-switch v-model="formData.anonymouslyPost" active-text="开启" inactive-text="关闭" />
      </el-form-item>

      <el-form-item label="群组类型" prop="groupType">
        <el-radio-group v-model="formData.groupType" @change="handleGroupTypeChange">
          <el-radio :label="1">已加入群组</el-radio>
          <el-radio :label="2">未加入群组</el-radio>
        </el-radio-group>
      </el-form-item>

      <!-- 已加入群组配置 -->
      <template v-if="formData.groupType === 1">
        <el-form-item label="每个账号发帖数" prop="groupsPerAccount">
          <el-input-number
            v-model="formData.groupsPerAccount"
            :min="1"
            :max="50"
            placeholder="每个账号发布的群组数量"
          />
        </el-form-item>

        <el-form-item label="选择群组">
          <el-button
            type="primary"
            @click="openGroupSelector"
            :disabled="formData.accountSelectionMode === 'AUTO' ? !formData.autoAccountCount : selectorAccountIds.length === 0"
          >
            <Icon icon="ep:search" class="mr-5px" /> 选择群组
          </el-button>
          <div class="text-sm text-gray-500 ml-2">根据账号和数量查询可用群组</div>
        </el-form-item>

        <!-- 已选群组展示 -->
        <el-form-item
          v-if="formData.selectedGroups && formData.selectedGroups.length > 0"
          label="已选群组"
        >
          <div class="w-full">
            <el-tag
              v-for="group in formData.selectedGroups.slice(0, 10)"
              :key="group.groupId"
              closable
              @close="removeSelectedGroup(group.groupId)"
              class="mr-5px mb-5px"
              size="small"
            >
              {{ group.groupName }}
            </el-tag>
            <span v-if="formData.selectedGroups.length > 10" class="text-sm text-gray-500">
              等{{ formData.selectedGroups.length }}个群组
            </span>
          </div>
        </el-form-item>
      </template>

      <!-- 未加入群组配置 -->
      <template v-else>
        <el-form-item label="选择群组">
          <el-button type="primary" @click="openUnjoinedGroupSelector">
            <Icon icon="ep:search" class="mr-5px" /> 选择未加入的群组
          </el-button>
        </el-form-item>

        <!-- 已选群组展示 -->
        <el-form-item
          v-if="formData.selectedUnjoinedGroups && formData.selectedUnjoinedGroups.length > 0"
          label="已选群组"
        >
          <div class="w-full">
            <el-tag
              v-for="group in formData.selectedUnjoinedGroups.slice(0, 10)"
              :key="group.id"
              closable
              @close="removeUnjoinedGroup(group.id)"
              class="mr-5px mb-5px"
              size="small"
            >
              {{ group.groupName }}
            </el-tag>
            <span v-if="formData.selectedUnjoinedGroups.length > 10" class="text-sm text-gray-500">
              等{{ formData.selectedUnjoinedGroups.length }}个群组
            </span>
          </div>
        </el-form-item>
      </template>

      <el-form-item label="发帖间隔">
        <div class="flex items-center gap-8px flex-nowrap">
          <el-input-number v-model="formData.minIntervalMinutes" :min="1" :max="60" controls-position="right" class="!w-130px" />
          <span class="text-gray-500 whitespace-nowrap">至</span>
          <el-input-number v-model="formData.maxIntervalMinutes" :min="formData.minIntervalMinutes" :max="60" controls-position="right" class="!w-130px" />
          <span class="text-gray-500 whitespace-nowrap">分钟（每条帖子依次执行）</span>
        </div>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="dialogVisible = false">取 消</el-button>
      <el-button type="primary" @click="submitForm" :loading="formLoading">确 定</el-button>
    </template>
  </Dialog>

  <!-- 群组选择器弹窗（已加入群组） -->
  <GroupPublishGroupSelector
    v-model="groupSelectorVisible"
    :selected-group-ids="selectedGroupIds"
    :account-ids="selectorAccountIds"
    :expected-account-count="formData.accountSelectionMode === 'AUTO' ? formData.autoAccountCount : selectorAccountIds.length"
    :groups-per-account="formData.groupsPerAccount"
    :resource-group-id="formData.resourceGroupId"
    :joined-before-days="formData.joinedBeforeDays"
    :account-selection-mode="formData.accountSelectionMode"
    :target-account-count="formData.autoAccountCount"
    action-type="group_post"
    @confirm="handleGroupConfirm"
  />

  <!-- 群组选择器弹窗（未加入群组） -->
  <GroupSelector
    v-model="unjoinedGroupSelectorVisible"
    :multiple="true"
    @confirm="handleUnjoinedGroupConfirm"
  />
</template>

<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { Plus } from '@element-plus/icons-vue'
import { Dialog } from '@/components/Dialog'
import GroupPublishGroupSelector from './GroupPublishGroupSelector.vue'
import GroupSelector from '../collect/components/GroupSelector.vue'
import * as OperationApi from '@/api/facebook/operation'
import { FbAccountApi, filterSelectableFbAccounts } from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import FbAccountSelector from '../components/FbAccountSelector.vue'
import type { FbCollectGroup } from '@/api/facebook/fbcollectgroup'

defineOptions({ name: 'GroupPublishForm' })

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('发群帖')
const formLoading = ref(false)
const accountGroups = ref<any[]>([])
const accounts = ref<any[]>([])
const groupSelectorVisible = ref(false)
const unjoinedGroupSelectorVisible = ref(false)

// 计算已选群组的ID列表
const selectedGroupIds = computed(() => {
  return formData.value.selectedGroups.map((g) => g.groupId)
})

const selectorAccountIds = computed(() => {
  if (formData.value.accountSelectionMode === 'MANUAL') {
    return formData.value.accountIds
  }
  return []
})

const formData = ref({
  accountIds: [] as string[],
  accountSelectionMode: 'AUTO' as 'AUTO' | 'MANUAL',
  autoAccountCount: undefined as number | undefined,
  postContent: '',
  mediaUrls: [] as string[], // 存储本地文件路径
  randomizeImagesAndAppendEmoji: true,
  anonymouslyPost: false,
  groupType: 1, // 1=已加入群组, 2=未加入群组
  groupsPerAccount: 5, // 每个账号发布的群组数量
  resourceGroupId: undefined as number | undefined,
  joinedBeforeDays: 3,
  selectedGroups: [] as any[], // 已选择的群组列表（已加入）
  selectedUnjoinedGroups: [] as FbCollectGroup[], // 已选择的群组列表（未加入）
  groupKeywords: '',
  minIntervalMinutes: 1,
  maxIntervalMinutes: 5
})

const formRules = reactive({
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }],
  postContent: [{ required: true, message: '请输入帖子内容', trigger: 'blur' }]
})

const formRef = ref()

/** 打开弹窗 */
const open = async () => {
  dialogVisible.value = true
  resetForm()
  await loadAccountGroups()
  await loadAccounts()
}
defineExpose({ open })

/** 加载账号分组 */
const loadAccountGroups = async () => {
  try {
    accountGroups.value = await AccountGroupApi.getAllEnabledGroups()
  } catch (error) {
    console.error('加载账号分组失败:', error)
  }
}

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    accounts.value = filterSelectableFbAccounts(data.list || [])
  } catch (error) {
    console.error('加载账号失败:', error)
  }
}

/** 选择文件 - 调用 WPF 文件选择对话框 */
const handleSelectFiles = async () => {
  try {
    // 检查是否在 WPF 环境中
    if (!window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      message.warning('请在 WPF 应用中使用此功能')
      return
    }

    // 调用 WPF 文件选择对话框
    const result = window.chrome.webview.hostObjects.sync.wpfBridge.SelectMediaFiles()
    const selectedFiles = JSON.parse(result) as string[]

    if (selectedFiles.length === 0) {
      return
    }

    // 限制最多10个文件
    if (selectedFiles.length > 10) {
      message.warning('最多只能选择10个文件')
      formData.value.mediaUrls = selectedFiles.slice(0, 10)
    } else {
      formData.value.mediaUrls = selectedFiles
    }

    message.success(`已选择 ${formData.value.mediaUrls.length} 个文件`)
    console.log('✅ 选择的文件:', formData.value.mediaUrls)
  } catch (error) {
    console.error('❌ 文件选择失败:', error)
    message.error(`文件选择失败: ${error instanceof Error ? error.message : String(error)}`)
  }
}

/** 获取文件名 */
const getFileName = (path: string) => {
  return path.split('\\').pop() || path.split('/').pop() || path
}

/** 移除文件 */
const removeFile = (index: number) => {
  formData.value.mediaUrls.splice(index, 1)
}

/** 清空文件 */
const clearFiles = () => {
  formData.value.mediaUrls = []
}

/** 账号变化时清空群组选择 */
const handleAccountChange = () => {
  formData.value.selectedGroups = []
}

/** 群组类型变化时清空选择 */
const handleGroupTypeChange = () => {
  formData.value.selectedGroups = []
  formData.value.selectedUnjoinedGroups = []
}

/** 打开群组选择器（已加入） */
const openGroupSelector = () => {
  if (formData.value.accountSelectionMode === 'AUTO' && !formData.value.autoAccountCount) {
    message.warning('请先填写自动分配的账号数量')
    return
  }
  if (formData.value.accountSelectionMode === 'MANUAL' && formData.value.accountIds.length === 0) {
    message.warning('请先选择执行账号')
    return
  }
  if (formData.value.accountSelectionMode === 'MANUAL' && selectorAccountIds.value.length === 0) {
    message.warning('暂无可用的执行账号')
    return
  }
  groupSelectorVisible.value = true
}

/** 打开群组选择器（未加入） */
const openUnjoinedGroupSelector = () => {
  unjoinedGroupSelectorVisible.value = true
}

/** 群组选择确认（已加入） */
const handleGroupConfirm = (groups: any[]) => {
  formData.value.selectedGroups = groups
}

/** 群组选择确认（未加入） */
const handleUnjoinedGroupConfirm = (groups: FbCollectGroup[]) => {
  formData.value.selectedUnjoinedGroups = groups
}

/** 移除已选群组（已加入） */
const removeSelectedGroup = (groupId: string) => {
  formData.value.selectedGroups = formData.value.selectedGroups.filter((g) => g.groupId !== groupId)
}

/** 移除已选群组（未加入） */
const removeUnjoinedGroup = (groupId: string) => {
  formData.value.selectedUnjoinedGroups = formData.value.selectedUnjoinedGroups.filter(
    (g) => g.id !== groupId
  )
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    accountIds: [],
    accountSelectionMode: 'AUTO',
    autoAccountCount: undefined,
    postContent: '',
    mediaUrls: [],
    randomizeImagesAndAppendEmoji: true,
    anonymouslyPost: false,
    groupType: 1, // 1=已加入群组, 2=未加入群组
    groupsPerAccount: 5, // 每个账号发布的群组数量
    resourceGroupId: undefined,
    joinedBeforeDays: 3,
    selectedGroups: [], // 已选择的群组列表（已加入）
    selectedUnjoinedGroups: [], // 已选择的群组列表（未加入）
    groupKeywords: '',
    minIntervalMinutes: 1,
    maxIntervalMinutes: 5
  }
  formRef.value?.resetFields()
}

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  const valid = await formRef.value?.validate()
  if (!valid) return

  try {
    formLoading.value = true

    if (formData.value.accountSelectionMode === 'AUTO' && !formData.value.autoAccountCount) {
      message.warning('请输入自动分配的账号数量')
      return
    }

    // 计算期望数量
    let expectedCount = 0
    if (formData.value.groupType === 1) {
      // 已加入群组：账号数 × 每组账号数
      expectedCount = selectorAccountIds.value.length * formData.value.groupsPerAccount
    } else {
      // 未加入群组：根据选择的群组数量
      expectedCount = formData.value.selectedUnjoinedGroups.length
    }

    // 构建任务数据
    const data = {
      taskType: 13, // 发群帖
      taskName: `发群帖-${new Date().getTime()}`, // 自动生成任务名称
      accountIds: formData.value.accountIds,
      accountSelectionMode: formData.value.accountSelectionMode,
      autoAccountCount: formData.value.autoAccountCount,
      expectedCount:
        formData.value.accountSelectionMode === 'AUTO'
          ? formData.value.autoAccountCount || 0
          : expectedCount,
      actionConfig: JSON.stringify({
        postContent: formData.value.postContent,
        mediaUrls: formData.value.mediaUrls,
        randomizeImagesAndAppendEmoji: formData.value.randomizeImagesAndAppendEmoji,
        anonymouslyPost: formData.value.anonymouslyPost,
        groupType: formData.value.groupType,
        groupsPerAccount: formData.value.groupsPerAccount,
        resourceGroupId: formData.value.resourceGroupId,
        joinedBeforeDays: formData.value.joinedBeforeDays,
        selectedGroups: formData.value.selectedGroups,
        selectedUnjoinedGroups: formData.value.selectedUnjoinedGroups.map((g) => ({
          groupId: g.id,
          groupName: g.groupName,
          groupUrl: g.url
        })),
        minIntervalMinutes: formData.value.minIntervalMinutes,
        maxIntervalMinutes: formData.value.maxIntervalMinutes
      })
    }

    await OperationApi.createFbOperationTask(data)
    message.success('发群帖任务已创建，已加入账号串行队列')

    dialogVisible.value = false
    emit('success')
  } catch (error) {
    console.error('创建任务失败:', error)
    message.error('创建失败')
  } finally {
    formLoading.value = false
  }
}
</script>

<style scoped lang="scss">
.text-sm {
  font-size: 12px;
}
.text-gray-500 {
  color: #9ca3af;
}
.mt-1 {
  margin-top: 4px;
}
</style>
