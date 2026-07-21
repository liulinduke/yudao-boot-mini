<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="900px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      v-loading="formLoading"
    >
      <div class="mb-12px">
        <el-alert
          title="帖子评论任务会按评论剩余额度平均分配，每个帖子最多只分配一个评论账号"
          type="warning"
          :closable="false"
          show-icon
        />
      </div>

      <el-form-item label="帖子来源" required>
        <el-radio-group v-model="postSourceMode">
          <el-radio label="manual">手动输入</el-radio>
          <el-radio label="select">从资源库选择</el-radio>
        </el-radio-group>
      </el-form-item>

      <el-form-item v-if="postSourceMode === 'manual'" label="帖子链接" prop="manualPostUrls">
        <el-input
          v-model="formData.manualPostUrls"
          type="textarea"
          :rows="6"
          placeholder="每行一个帖子链接"
        />
      </el-form-item>

      <el-form-item v-else label="资源帖子" prop="selectedPosts">
        <div class="w-full">
          <el-button type="primary" @click="postSelectorVisible = true">
            <Icon icon="ep:plus" class="mr-5px" /> 选择帖子
          </el-button>
          <div v-if="selectedPosts.length > 0" class="mt-2">
            <el-tag
              v-for="post in selectedPosts.slice(0, 8)"
              :key="post.id"
              closable
              class="mr-2 mb-2"
              @close="removeSelectedPost(post.id)"
            >
              {{ post.postUser || '帖子' }} - {{ (post.postContent || post.url || '').slice(0, 24) }}
            </el-tag>
            <div class="text-gray-500 text-sm">已选择 {{ selectedPosts.length }} 个帖子</div>
          </div>
        </div>
      </el-form-item>

      <el-form-item label="执行账号" prop="accountIds">
        <el-select
          v-model="formData.accountIds"
          multiple
          filterable
          style="width: 100%"
          placeholder="请选择执行账号"
        >
          <el-option
            v-for="account in accounts"
            :key="account.id"
            :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
            :value="String(account.id)"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="执行项" required>
        <div class="w-full">
          <el-checkbox-group v-model="selectedActions">
            <el-checkbox :label="1">点赞</el-checkbox>
            <el-checkbox :label="6">评论</el-checkbox>
          </el-checkbox-group>
          <div v-if="commentQuotaSummary" class="mt-2 text-sm text-gray-500">
            {{ commentQuotaSummary }}
          </div>
        </div>
      </el-form-item>

      <el-form-item v-if="selectedActions.includes(6)" label="评论话术" required>
        <div class="w-full">
          <el-radio-group v-model="commentScriptType" class="mb-2">
            <el-radio :label="1">手动输入</el-radio>
            <el-radio :label="2">从话术库选择</el-radio>
          </el-radio-group>

          <el-input
            v-if="commentScriptType === 1"
            v-model="commentManualScripts"
            type="textarea"
            :rows="4"
            placeholder="每行一条评论话术"
          />

          <div v-else>
            <el-button type="primary" @click="openCommentScriptSelector" class="mb-2">
              <Icon icon="ep:plus" class="mr-5px" /> 从话术库选择
            </el-button>
            <div v-if="commentSelectedScripts.length > 0">
              <el-tag
                v-for="(script, index) in commentSelectedScripts.slice(0, 6)"
                :key="index"
                closable
                class="mr-2 mb-2"
                size="small"
                @close="removeCommentScript(index)"
              >
                {{ script.slice(0, 30) }}
              </el-tag>
            </div>
          </div>

          <el-checkbox v-model="commentAppendRandomEmoji" class="mt-2">
            追加随机表情
          </el-checkbox>
        </div>
      </el-form-item>

      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" :rows="3" placeholder="可选" />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button type="primary" :loading="formLoading" @click="submitForm">确定</el-button>
      <el-button @click="dialogVisible = false">取消</el-button>
    </template>
  </Dialog>

  <PostSelector v-model="postSelectorVisible" @confirm="handlePostConfirm" />
  <ScriptSelector
    v-model="commentScriptSelectorVisible"
    :multiple="true"
    @confirm="handleCommentScriptConfirm"
  />
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import PostSelector from '../collect/components/PostSelector.vue'
import ScriptSelector from './dmtask/ScriptSelector.vue'
import { FbAccountApi, filterSelectableFbAccounts } from '@/api/facebook/account'
import { DailyLimitApi } from '@/api/facebook/dailylimit'
import {
  createFbOperationTask,
  type FbOperationTaskSaveReqVO
} from '@/api/facebook/operation'

defineOptions({ name: 'PostCommentForm' })

const emit = defineEmits(['success'])
const message = useMessage()

const TASK_TYPE = 15
const dialogVisible = ref(false)
const dialogTitle = ref('创建帖子评论任务')
const formLoading = ref(false)
const formRef = ref()
const accounts = ref<any[]>([])
const postSelectorVisible = ref(false)
const commentScriptSelectorVisible = ref(false)
const postSourceMode = ref<'manual' | 'select'>('manual')
const selectedPosts = ref<any[]>([])
const selectedActions = ref<number[]>([])
const commentScriptType = ref(1)
const commentManualScripts = ref('')
const commentSelectedScripts = ref<string[]>([])
const commentAppendRandomEmoji = ref(false)
const commentRemainingMap = ref<Record<string, number>>({})

const formData = ref({
  accountIds: [] as string[],
  manualPostUrls: '',
  selectedPosts: [] as any[],
  remark: ''
})

const normalizeManualUrls = (raw: string) =>
  Array.from(
    new Set(
      String(raw || '')
        .split(/\r?\n/)
        .map((item) => item.trim())
        .filter((item) => /^https?:\/\//i.test(item))
    )
  )

const formRules = reactive({
  manualPostUrls: [
    {
      validator: (_rule: any, value: string, callback: any) => {
        if (postSourceMode.value !== 'manual') return callback()
        if (normalizeManualUrls(value).length === 0) {
          callback(new Error('请至少输入一个帖子链接'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ],
  selectedPosts: [
    {
      validator: (_rule: any, _value: any, callback: any) => {
        if (postSourceMode.value !== 'select') return callback()
        if (selectedPosts.value.length === 0) {
          callback(new Error('请至少选择一个帖子'))
          return
        }
        callback()
      },
      trigger: 'change'
    }
  ],
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }]
})

const getPostUrls = () =>
  postSourceMode.value === 'manual'
    ? normalizeManualUrls(formData.value.manualPostUrls)
    : Array.from(new Set(selectedPosts.value.map((item) => String(item.url || '').trim()).filter(Boolean)))

const commentScripts = computed(() => {
  if (commentScriptType.value === 1) {
    return Array.from(
      new Set(
        commentManualScripts.value
          .split(/\r?\n/)
          .map((item) => item.trim())
          .filter(Boolean)
      )
    )
  }
  return commentSelectedScripts.value
})

const commentQuotaSummary = computed(() => {
  if (!selectedActions.value.includes(6)) return ''
  const accountIds = formData.value.accountIds || []
  const totalRemaining = accountIds.reduce(
    (sum, accountId) => sum + (commentRemainingMap.value[String(accountId)] || 0),
    0
  )
  const totalPosts = getPostUrls().length
  if (accountIds.length === 0) return '评论额度预检查：请先选择执行账号'
  return `评论额度预检查：剩余 ${totalRemaining} 次，可分配评论 ${Math.min(totalRemaining, totalPosts)} / ${totalPosts} 个帖子`
})

const loadAccounts = async () => {
  const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
  accounts.value = filterSelectableFbAccounts(data?.list || [])
}

const loadCommentRemaining = async () => {
  const accountIds = formData.value.accountIds || []
  const nextMap: Record<string, number> = {}
  for (const accountId of accountIds) {
    try {
      const resp = await DailyLimitApi.getRemainingCount(String(accountId), 'comment')
      nextMap[String(accountId)] = Number(resp?.data ?? resp ?? 0)
    } catch {
      nextMap[String(accountId)] = 0
    }
  }
  commentRemainingMap.value = nextMap
}

watch(
  () => [...formData.value.accountIds],
  () => {
    loadCommentRemaining()
  }
)

const open = async () => {
  dialogVisible.value = true
  resetForm()
  await loadAccounts()
}

defineExpose({ open })

const handlePostConfirm = (posts: any[]) => {
  selectedPosts.value = posts || []
  formData.value.selectedPosts = [...selectedPosts.value]
  message.success(`已选择 ${selectedPosts.value.length} 个帖子`)
}

const removeSelectedPost = (postId: number) => {
  selectedPosts.value = selectedPosts.value.filter((item) => item.id !== postId)
  formData.value.selectedPosts = [...selectedPosts.value]
}

const openCommentScriptSelector = () => {
  commentScriptSelectorVisible.value = true
}

const handleCommentScriptConfirm = (scripts: any[] | any) => {
  const list = Array.isArray(scripts) ? scripts : [scripts]
  for (const script of list) {
    const content = script?.scriptContent?.trim()
    if (content && !commentSelectedScripts.value.includes(content)) {
      commentSelectedScripts.value.push(content)
    }
  }
}

const removeCommentScript = (index: number) => {
  commentSelectedScripts.value.splice(index, 1)
}

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  const postUrls = getPostUrls()
  if (postUrls.length === 0) {
    message.warning('请至少提供一个帖子链接')
    return
  }
  if (selectedActions.value.length === 0) {
    message.warning('请至少选择一个执行项')
    return
  }
  if (selectedActions.value.includes(6) && commentScripts.value.length === 0) {
    message.warning('已勾选评论，请提供评论话术')
    return
  }

  formLoading.value = true
  try {
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

    const configData = {
      sourceMode: postSourceMode.value,
      actions: selectedActions.value,
      postUrls,
      commentScripts: commentScripts.value,
      commentAppendRandomEmoji: commentAppendRandomEmoji.value
    }

    const submitData: FbOperationTaskSaveReqVO = {
      taskType: TASK_TYPE,
      taskName: `帖子评论_${timestamp}`,
      accountIds: formData.value.accountIds,
      postUrls,
      postUrl: postUrls[0],
      actionConfig: JSON.stringify(configData),
      expectedCount: selectedActions.value.includes(6)
        ? Math.min(
            postUrls.length,
            formData.value.accountIds.reduce(
              (sum, accountId) => sum + (commentRemainingMap.value[String(accountId)] || 0),
              0
            )
          ) + (selectedActions.value.includes(1) ? postUrls.length : 0)
        : postUrls.length,
      remark: formData.value.remark
    }

    await createFbOperationTask(submitData)
    message.success('帖子评论任务已创建，已加入账号串行队列')
    dialogVisible.value = false
    emit('success')
  } finally {
    formLoading.value = false
  }
}

const resetForm = () => {
  formData.value = {
    accountIds: [],
    manualPostUrls: '',
    selectedPosts: [],
    remark: ''
  }
  postSourceMode.value = 'manual'
  selectedPosts.value = []
  selectedActions.value = []
  commentScriptType.value = 1
  commentManualScripts.value = ''
  commentSelectedScripts.value = []
  commentAppendRandomEmoji.value = false
  commentRemainingMap.value = {}
  formRef.value?.resetFields()
}
</script>

<style scoped lang="scss">
:deep(.el-checkbox) {
  margin-right: 20px;
}
</style>
