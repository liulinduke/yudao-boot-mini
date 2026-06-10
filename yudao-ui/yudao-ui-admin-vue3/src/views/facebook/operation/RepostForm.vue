<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="900px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="140px"
      v-loading="formLoading"
    >
      <!-- 风控警告 -->
      <div style="margin-bottom: 12px">
        <el-alert
          title="建议每个账号每日转帖操作不超过10次，避免触发风控机制"
          type="warning"
          :closable="false"
          show-icon
        />
      </div>

      <!-- 帖子链接 -->
      <el-form-item label="帖子链接" prop="postUrl">
        <el-input
          v-model="formData.postUrl"
          placeholder="请输入Facebook帖子链接"
          type="textarea"
          :rows="2"
        />
      </el-form-item>

      <!-- 执行账号 -->
      <el-form-item label="执行账号" prop="accountIds">
        <el-select
          v-model="formData.accountIds"
          multiple
          placeholder="请选择执行账号"
          style="width: 100%"
          filterable
        >
          <el-option
            v-for="account in accounts"
            :key="account.id"
            :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
            :value="account.id"
          />
        </el-select>
      </el-form-item>

      <!-- 执行项配置 -->
      <el-form-item label="执行项" prop="actionConfig">
        <div class="w-full">
          <!-- 第一行：点赞、转发到动态消息 -->
          <div class="mb-10px flex items-center">
            <el-checkbox v-model="selectedActions" :label="1" class="mr-20px">点赞</el-checkbox>
            <el-checkbox v-model="selectedActions" :label="2">转发到动态消息</el-checkbox>
          </div>

          <el-form-item
            v-if="selectedActions.includes(2)"
            label="动态附言"
            class="!mb-10px"
            label-width="140px"
          >
            <el-input
              v-model="actionConfig.feedMessage"
              type="textarea"
              :rows="2"
              placeholder="Share now 时附带的文字（可选）"
            />
          </el-form-item>

          <!-- 第二行：转贴到好友 + 数量 -->
          <div class="mb-10px">
            <el-checkbox v-model="selectedActions" :label="4"> 转贴到好友 </el-checkbox>
            <el-input-number
              v-if="selectedActions.includes(4)"
              v-model="actionConfig.shareToFriendCount"
              :min="1"
              :max="100"
              size="small"
              class="ml-10px"
            />
          </div>

          <el-form-item
            v-if="selectedActions.includes(4)"
            label="好友附言"
            class="!mb-10px"
            label-width="140px"
          >
            <el-input
              v-model="actionConfig.friendMessage"
              type="textarea"
              :rows="2"
              placeholder="Messenger 发送给好友时的附言（可选）"
            />
          </el-form-item>

          <!-- 转发到群组 + 数量 + 选择按钮 -->
          <div class="mb-10px">
            <el-checkbox v-model="selectedActions" :label="5"> 转发到群组 </el-checkbox>
            <el-input-number
              v-if="selectedActions.includes(5)"
              v-model="actionConfig.shareToGroupCount"
              :min="1"
              :max="100"
              size="small"
              class="ml-10px"
            />
            <el-button
              v-if="selectedActions.includes(5)"
              type="primary"
              size="small"
              class="ml-10px"
              @click="openGroupSelector"
            >
              选择群组
            </el-button>
          </div>

          <!-- 已选群组展示 -->
          <div v-if="selectedGroups.length > 0" class="mt-10px">
            <div class="text-sm text-gray-600 mb-5px"
              >已选择 {{ selectedGroups.length }} 个群组：</div
            >
            <el-tag
              v-for="group in selectedGroups.slice(0, 10)"
              :key="group.groupId"
              closable
              @close="removeSelectedGroup(group.groupId)"
              class="mr-5px mb-5px"
              size="small"
            >
              {{ group.groupName }}
            </el-tag>
            <span v-if="selectedGroups.length > 10" class="text-sm text-gray-500">
              等{{ selectedGroups.length }}个群组
            </span>
          </div>

          <el-form-item
            v-if="selectedActions.includes(5)"
            label="群组附言"
            class="!mb-10px"
            label-width="140px"
          >
            <el-input
              v-model="actionConfig.groupMessage"
              type="textarea"
              :rows="2"
              placeholder="转发到群组时的附言（可选）"
            />
          </el-form-item>
        </div>
      </el-form-item>

      <!-- 备注 -->
      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button type="primary" @click="submitForm" :loading="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>

  <!-- 群组选择器 -->
  <GroupSelectorForRepost
    v-model="groupSelectorVisible"
    :selected-group-ids="selectedGroupIds"
    :account-ids="formData.accountIds"
    @confirm="handleGroupConfirm"
  />

</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi } from '@/api/facebook/account'
import {
  createFbOperationTask,
  getFbOperationTask,
  FbOperationTaskSaveReqVO
} from '@/api/facebook/operation'
import { startBrowserCollect } from '@/utils/wpfBridge'
import GroupSelectorForRepost from './GroupSelectorForRepost.vue'

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('创建转帖任务')
const formLoading = ref(false)
const formRef = ref()

// 表单数据
const formData = ref({
  postUrl: '',
  accountIds: [] as string[],
  remark: ''
})

// 执行项配置
const selectedActions = ref<number[]>([])
const actionConfig = ref({
  feedMessage: '',
  friendMessage: '',
  groupMessage: '',
  shareToFriendCount: 10,
  shareToGroupCount: 1
})

// 群组选择
const selectedGroups = ref<any[]>([])
const groupSelectorVisible = ref(false)

// 账号列表
const accounts = ref<any[]>([])

// 表单验证规则
const formRules = reactive({
  postUrl: [
    { required: true, message: '请输入帖子链接', trigger: 'blur' },
    { pattern: /^https?:\/\/.*/, message: '请输入有效的URL', trigger: 'blur' }
  ],
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }]
})

// 计算已选群组ID列表
const selectedGroupIds = computed(() => {
  return selectedGroups.value.map((g) => g.groupId)
})

/** 打开弹窗 */
const open = async () => {
  dialogVisible.value = true
  resetForm()
  await loadAccounts()
}

defineExpose({ open })

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    if (data && data.list && Array.isArray(data.list)) {
      accounts.value = data.list
    } else {
      accounts.value = []
    }
  } catch (error) {
    console.error('加载账号列表失败:', error)
    accounts.value = []
  }
}

/** 打开群组选择器 */
const openGroupSelector = () => {
  groupSelectorVisible.value = true
}

/** 移除已选群组 */
const removeSelectedGroup = (groupId: string) => {
  const index = selectedGroups.value.findIndex((g) => g.groupId === groupId)
  if (index > -1) {
    selectedGroups.value.splice(index, 1)
  }
}

/** 确认群组选择 */
const handleGroupConfirm = (groups: any[]) => {
  selectedGroups.value = groups
  message.success(`已选择 ${groups.length} 个群组`)
}

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  if (!formRef.value) return

  // 验证执行项
  if (selectedActions.value.length === 0) {
    message.warning('请至少选择一个执行项')
    return
  }

  // 验证转发到群组的群组选择
  if (selectedActions.value.includes(5) && selectedGroups.value.length === 0) {
    message.warning('请选择要转发到的群组')
    return
  }

  await formRef.value.validate()

  formLoading.value = true
  try {
    // 生成任务名称
    const timestamp = new Date()
      .toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      })
      .replace(/[\/\s:]/g, '')

    // 构建执行项配置JSON
    const configData = {
      actions: selectedActions.value.filter((a) => a !== 3),
      feedMessage: actionConfig.value.feedMessage,
      friendMessage: actionConfig.value.friendMessage,
      groupMessage: actionConfig.value.groupMessage,
      shareToFriendCount: actionConfig.value.shareToFriendCount,
      shareToGroupCount: actionConfig.value.shareToGroupCount,
      selectedGroups: selectedGroups.value
    }

    // 计算期望数量
    let expectedCount = 0
    if (selectedActions.value.includes(1)) expectedCount += formData.value.accountIds.length // 点赞：每个账号1次
    if (selectedActions.value.includes(2)) expectedCount += formData.value.accountIds.length // 转发到动态消息
    if (selectedActions.value.includes(4))
      expectedCount += actionConfig.value.shareToFriendCount * formData.value.accountIds.length // 转贴到好友：可配置次数
    if (selectedActions.value.includes(5))
      expectedCount += selectedGroups.value.length * formData.value.accountIds.length // 转发到群组：按选择的群组数量

    // 根据话术类型设置参数
    const submitData = {
      taskType: 10, // 转贴任务（运营任务从10开始，避免与采集任务冲突）
      taskName: `转帖_${timestamp}`,
      accountIds: formData.value.accountIds,
      postUrl: formData.value.postUrl,
      actionConfig: JSON.stringify(configData),
      expectedCount: expectedCount,
      remark: formData.value.remark
    } as unknown as FbOperationTaskSaveReqVO

    const result = await createFbOperationTask(submitData)
    const respData = result.data || result
    const taskId = respData?.id || respData
    
    message.success('任务创建成功')
    
    // 转帖任务创建成功后启动浏览器执行
    if (taskId) {
      const createdTaskDetail = await getFbOperationTask(String(taskId))
      const createdDetails = createdTaskDetail?.details || []
      const repostConfig = JSON.stringify({
        taskId: String(taskId),
        postUrl: formData.value.postUrl,
        actionConfig: configData
      })

      const startedAccounts = new Set<string>()
      for (const accountId of formData.value.accountIds) {
        if (startedAccounts.has(accountId)) continue

        const accountInfo = accounts.value.find((acc) => String(acc.id) === String(accountId))
        if (!accountInfo) continue

        const detail = createdDetails.find((d) => String(d.accountId) === String(accountId))
        const cookie = accountInfo.cookie || null

        try {
          startBrowserCollect(
            String(detail?.id || `${taskId}_${accountId}`),
            String(accountId),
            cookie,
            formData.value.postUrl,
            expectedCount,
            10,
            repostConfig,
            true
          )
          startedAccounts.add(accountId)
          console.log(`🚀 启动转帖: 任务=${taskId}, 明细=${detail?.id}, 账号=${accountId}`)
        } catch (error) {
          console.error(`启动账号 ${accountId} 的转帖任务失败:`, error)
        }
      }

      if (startedAccounts.size > 0) {
        message.success(`已启动 ${startedAccounts.size} 个账号的浏览器执行转帖任务`)
      } else {
        message.warning('任务已创建，但 WPF 未连接或账号无效')
      }
    }
    
    dialogVisible.value = false
    emit('success')
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    postUrl: '',
    accountIds: [],
    remark: ''
  }
  selectedActions.value = []
  actionConfig.value = {
    feedMessage: '',
    friendMessage: '',
    groupMessage: '',
    shareToFriendCount: 10,
    shareToGroupCount: 1
  }
  selectedGroups.value = []
  formRef.value?.resetFields()
}
</script>

<style scoped lang="scss">
:deep(.el-checkbox) {
  margin-right: 20px;
  margin-bottom: 10px;
}
</style>
