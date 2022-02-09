# How to contribute in the project and submit your own code

## Contributor License Agreements

This is the CLA required to be signed by all contributors:
https://gist.github.com/rogeralsing/4f4eed4d0b583da04ab6d0b48a3b9abf


> At Asynkron, we manage a lot of open source projects and we’re required to have agreements with everyone who takes part in them.
It’s the easiest way for you to give us permission to use your contributions. 
In effect, you’re giving us a licence, but you still own the copyright — so you retain the right to modify your code and use it in other projects.


## Contribution policies

### General policies

1. Project is using [semantic versioning](https://semver.org/)

2. Breaking changes should be avoided if possible

3. All stable versions should have detailed change log

4. In case of major version change, migration guidance should be prepared

### Creating new features / experimenting with existing one

1. Experimental features live on feature branches and get their own unstable packages

2. Feature branches packages are pushed to GitHub artifact storage

3. Changes on feature branches must have proper test coverage and all the tests must pass before merge to trunk

4. Feature branches merged to trunk have stable API and can be tested by wider audience

5. Features merge should be coordinated to align with roadmap versions plan

6. New features in trunk are alpha until deemed fit for stable release

7. Changes treated as alpha are pushed to nuget with explicit alpha version

8. Trunk version is treated as stable when:
   * Documentation is prepared
   * All tests are green
   * TODO

9. All the changes must be aligned with the planned roadmap

10. Roadmap plan is prepared based on community feedback
