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
          <!-- 第一行：点赞、转发到动态、转帖到个人中心 -->
          <div class="mb-10px flex items-center">
            <el-checkbox v-model="selectedActions" :label="1" class="mr-20px">点赞</el-checkbox>
            <el-checkbox v-model="selectedActions" :label="2" class="mr-20px"
              >转发到动态</el-checkbox
            >
            <el-checkbox v-model="selectedActions" :label="3">转帖到个人中心</el-checkbox>
          </div>

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

          <!-- 第三行：转发到群组 + 数量 + 选择按钮 -->
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
        </div>
      </el-form-item>

      <!-- 评论话术配置 -->
      <el-form-item label="评论话术" prop="commentScript">
        <div class="w-full">
          <!-- 选择方式 -->
          <el-radio-group v-model="commentScriptType" class="mb-10px">
            <el-radio label="manual">手动输入</el-radio>
            <el-radio label="library">从话术库选择</el-radio>
          </el-radio-group>

          <!-- 手动输入模式 -->
          <el-input
            v-if="commentScriptType === 'manual'"
            v-model="formData.commentScript"
            type="textarea"
            :rows="3"
            placeholder="请输入评论话术内容"
          />

          <!-- 话术库选择模式 -->
          <div v-else>
            <el-button type="primary" @click="openScriptSelector">
              <Icon icon="ep:search" class="mr-5px" /> 选择话术
            </el-button>

            <!-- 已选话术展示 -->
            <div v-if="selectedScriptInfo" class="mt-10px p-10px bg-gray-50 rounded">
              <div class="flex justify-between items-start">
                <div class="flex-1">
                  <div class="text-sm font-medium mb-5px">{{
                    selectedScriptInfo.scriptTitle || '无标题'
                  }}</div>
                  <div class="text-xs text-gray-600 line-clamp-2">{{
                    selectedScriptInfo.scriptContent
                  }}</div>
                </div>
                <el-tag size="small" class="ml-10px">
                  {{ getContentTypeText(selectedScriptInfo.contentType) }}
                </el-tag>
              </div>
            </div>
            <div v-else class="mt-10px text-sm text-gray-400"> 未选择话术 </div>
          </div>
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

  <!-- 话术选择器 -->
  <ScriptSelector v-model="scriptSelectorVisible" @confirm="handleScriptConfirm" />
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi } from '@/api/facebook/account'
import { ScriptApi } from '@/api/facebook/script'
import { FbOperationAddGroupResultApi } from '@/api/facebook/operation/addgroupresult'
import { createFbOperationTask, FbOperationTaskSaveReqVO } from '@/api/facebook/operation'
import GroupSelectorForRepost from './GroupSelectorForRepost.vue'
import ScriptSelector from './ScriptSelector.vue'

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
  commentScript: '',
  scriptLibraryId: undefined as number | undefined,
  remark: ''
})

// 评论话术类型：manual-手动输入，library-话术库
const commentScriptType = ref<'manual' | 'library'>('manual')

// 执行项配置
const selectedActions = ref<number[]>([])
const actionConfig = ref({
  shareToProfileCount: 1,
  shareToFriendCount: 10,
  shareToGroupCount: 1
})

// 群组选择
const selectedGroups = ref<any[]>([])
const groupSelectorVisible = ref(false)

// 话术选择
const scriptSelectorVisible = ref(false)
const selectedScriptInfo = ref<any>(null)

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

/** 打开话术选择器 */
const openScriptSelector = () => {
  scriptSelectorVisible.value = true
}

/** 确认话术选择 */
const handleScriptConfirm = (script: any) => {
  formData.value.scriptLibraryId = script.id
  selectedScriptInfo.value = script
  message.success(`已选择话术：${script.scriptTitle || '无标题'}`)
}

/** 获取内容类型文本 */
const getContentTypeText = (type: number) => {
  const typeMap: Record<number, string> = {
    1: '文本',
    2: '图文',
    3: '视频',
    4: '音频'
  }
  return typeMap[type] || '未知'
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
      actions: selectedActions.value,
      shareToProfileCount: actionConfig.value.shareToProfileCount,
      shareToGroupCount: actionConfig.value.shareToGroupCount,
      selectedGroups: selectedGroups.value
    }

    // 计算期望数量
    let expectedCount = 0
    if (selectedActions.value.includes(1)) expectedCount += formData.value.accountIds.length
    if (selectedActions.value.includes(2)) expectedCount += formData.value.accountIds.length
    if (selectedActions.value.includes(3)) expectedCount += formData.value.accountIds.length // 转帖到个人中心不需要数量
    if (selectedActions.value.includes(4))
      expectedCount += actionConfig.value.shareToFriendCount * formData.value.accountIds.length
    if (selectedActions.value.includes(5))
      expectedCount += selectedGroups.value.length * formData.value.accountIds.length

    // 根据话术类型设置参数
    const submitData = {
      taskType: 2, // 转贴任务
      taskName: `转帖_${timestamp}`,
      accountIds: formData.value.accountIds,
      postUrl: formData.value.postUrl,
      actionConfig: JSON.stringify(configData),
      commentScript: commentScriptType.value === 'manual' ? formData.value.commentScript : '',
      scriptLibraryId:
        commentScriptType.value === 'library' ? formData.value.scriptLibraryId : undefined,
      expectedCount: expectedCount,
      remark: formData.value.remark
    } as unknown as FbOperationTaskSaveReqVO

    await createFbOperationTask(submitData)
    message.success('任务创建成功')
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
    commentScript: '',
    scriptLibraryId: undefined,
    remark: ''
  }
  selectedActions.value = []
  actionConfig.value = {
    shareToProfileCount: 1,
    shareToFriendCount: 10,
    shareToGroupCount: 1
  }
  selectedGroups.value = []
  selectedScriptInfo.value = null
  commentScriptType.value = 'manual'
  formRef.value?.resetFields()
}
</script>

<style scoped lang="scss">
:deep(.el-checkbox) {
  margin-right: 20px;
  margin-bottom: 10px;
}
</style>
