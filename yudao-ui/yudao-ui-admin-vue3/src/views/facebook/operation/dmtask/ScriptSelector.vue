<template>
  <Dialog v-model="visible" title="选择话术（可多选）" width="800px">
    <div v-loading="loading">
      <el-form :inline="true" class="mb-10px">
        <el-form-item label="话术标题">
          <el-input
            v-model="searchKeyword"
            placeholder="请输入话术标题"
            clearable
            @keyup.enter="loadScripts"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadScripts">搜索</el-button>
        </el-form-item>
      </el-form>

      <el-table
        ref="tableRef"
        :data="scriptList"
        stripe
        border
        max-height="400"
        row-key="id"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="50" align="center" reserve-selection />
        <el-table-column label="标题" prop="scriptTitle" min-width="120" show-overflow-tooltip />
        <el-table-column label="内容" prop="scriptContent" min-width="300" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.scriptContent }}
          </template>
        </el-table-column>
      </el-table>

      <div v-if="selectedScripts.length > 0" class="mt-3 text-sm text-gray-500">
        已选择 {{ selectedScripts.length }} 条话术
      </div>

      <el-pagination
        v-if="total > 0"
        class="mt-4"
        v-model:current-page="queryParams.pageNo"
        v-model:page-size="queryParams.pageSize"
        :total="total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next, jumper"
        @size-change="loadScripts"
        @current-change="loadScripts"
      />
    </div>

    <template #footer>
      <el-button type="primary" @click="handleConfirm">确 定</el-button>
      <el-button @click="visible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ScriptApi, FbScriptVO } from '@/api/facebook/script'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits(['update:modelValue', 'confirm'])

const visible = ref(false)
const loading = ref(false)
const tableRef = ref()
const searchKeyword = ref('')
const scriptList = ref<FbScriptVO[]>([])
const total = ref(0)
const selectedScripts = ref<FbScriptVO[]>([])

const queryParams = ref({
  pageNo: 1,
  pageSize: 10,
  scriptTitle: ''
})

watch(
  () => props.modelValue,
  (val) => {
    visible.value = val
    if (val) {
      selectedScripts.value = []
      tableRef.value?.clearSelection?.()
      loadScripts()
    }
  }
)

watch(visible, (val) => {
  emit('update:modelValue', val)
})

const loadScripts = async () => {
  loading.value = true
  try {
    queryParams.value.scriptTitle = searchKeyword.value
    const data = await ScriptApi.getScriptPage(queryParams.value)
    scriptList.value = data.list || []
    total.value = data.total || 0
  } catch (error) {
    console.error('加载话术列表失败:', error)
    scriptList.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

const handleSelectionChange = (rows: FbScriptVO[]) => {
  selectedScripts.value = rows
}

const handleConfirm = () => {
  if (selectedScripts.value.length === 0) {
    message.warning('请至少选择一条话术')
    return
  }
  emit('confirm', selectedScripts.value)
  visible.value = false
}
</script>
