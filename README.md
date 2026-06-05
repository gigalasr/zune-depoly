# Zune Deploy
_This project is not afiliated with or endorsed by microsoft_

CLI tool for deploying XNA applications to the Zune on linux.


## Progress
Main:
- [x] Deploy Channel
- [ ] CLI
- [ ] Progress Callback for file uploads
- [x] Launch Channel
- [x] Runtime Deploy Channel 
- [ ] Container Representation 
  - [ ] Folder Based
    - [ ] With CFG    
    - [ ] With JSON
  - [ ] .ccgame 

Meta:
- [ ] GitHub CI for tests
- [ ] GitHub CI for AppImage
- [ ] Support for windows and macos? (Only needs the correct cmake flags for aftl I think?)
- [ ] Better Readme 
- [ ] Increase test code cov
- [ ] System tests from captured data?

## Build
```shell
dotnet build
```


---

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.