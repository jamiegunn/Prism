import { defineConfig } from 'orval'

export default defineConfig({
  prism: {
    input: {
      target: 'http://localhost:5000/swagger/v1/swagger.json',
    },
    output: {
      mode: 'tags-split',
      target: 'src/services/generated',
      schemas: 'src/services/generated/models',
      client: 'react-query',
      override: {
        mutator: {
          path: './src/services/apiClient.ts',
          name: 'customInstance',
        },
      },
    },
  },
})
