module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  testMatch: ['**/__tests__/**/*.test.ts'],
  testTimeout: 30000,
  globals: {
    'ts-jest': {
      tsconfig: { strict: false }
    }
  }
};
